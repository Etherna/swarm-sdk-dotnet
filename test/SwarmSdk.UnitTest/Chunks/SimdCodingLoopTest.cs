// Copyright 2021-present Etherna SA
// This file is part of SwarmSDK.
// 
// SwarmSDK is free software: you can redistribute it and/or modify it under the terms of the
// GNU Lesser General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// SwarmSDK is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License along with SwarmSDK.
// If not, see <https://www.gnu.org/licenses/>.

using ReedSolomon.NET.Loops;
using System;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using Xunit;

namespace Etherna.SwarmSdk.Chunks
{
    public class SimdCodingLoopTest
    {
        // Data.
        //the instruction set by its value: the enum is internal to the library
        public static TheoryData<int, int, int> CodingRanges
        {
            get
            {
                var data = new TheoryData<int, int, int>();
                foreach (var instructionSet in Enum.GetValues<SimdCodingLoop.InstructionSet>())
                foreach (var byteCount in new[] { 0, 1, 15, 16, 17, 31, 32, 33, 47, 48, 4104 })
                foreach (var offset in new[] { 0, 3 })
                    data.Add((int)instructionSet, byteCount, offset);
                return data;
            }
        }

        // Tests.
        /// <summary>
        /// Every coefficient of the field, on the vector lengths of each instruction set and on their remainders
        /// </summary>
        [Theory]
        [MemberData(nameof(CodingRanges))]
        public void CodeSomeShardsGivesTheShardsOfTheLibraryLoop(int instructionSetValue, int byteCount, int offset)
        {
            // Setup.
            var instructionSet = (SimdCodingLoop.InstructionSet)instructionSetValue;
            //a set not supported here is skipped: its coverage comes from the machines that have it
            if (instructionSet == SimdCodingLoop.InstructionSet.Avx2 && !Avx2.IsSupported ||
                instructionSet == SimdCodingLoop.InstructionSet.Ssse3 && !Ssse3.IsSupported)
                return;
            const int inputCount = 16;
            const int outputCount = 16;
            var random = new Random(byteCount * 10 + offset);
            var matrixRows = Enumerable.Range(0, outputCount)
                .Select(o => Enumerable.Range(0, inputCount).Select(i => (byte)(o * inputCount + i)).ToArray())
                .ToArray();
            var inputs = Enumerable.Range(0, inputCount)
                .Select(_ =>
                {
                    var input = new byte[offset + byteCount + 5];
                    random.NextBytes(input);
                    return input;
                })
                .ToArray();
            byte[][] BuildOutputs() => Enumerable.Range(0, outputCount)
                .Select(_ => Enumerable.Repeat((byte)0xA5, offset + byteCount + 5).ToArray())
                .ToArray();
            var expected = BuildOutputs();
            new InputOutputByteTableCodingLoop().CodeSomeShards(
                matrixRows, inputs, inputCount, expected, outputCount, offset, byteCount);
            var outputs = BuildOutputs();

            // Run.
            new SimdCodingLoop(instructionSet).CodeSomeShards(
                matrixRows, inputs, inputCount, outputs, outputCount, offset, byteCount);

            // Assert.
            //the bytes out of the range keep their value, in the expected outputs as in these
            Assert.Equal(expected, outputs);
        }

        [Fact]
        public void ConstructorRejectsAnInstructionSetNotSupported()
        {
            // Setup.
            //on a processor that supports AVX2 there is nothing to reject
            if (Avx2.IsSupported)
                return;

            // Run.
            Assert.Throws<PlatformNotSupportedException>(
                () => new SimdCodingLoop(SimdCodingLoop.InstructionSet.Avx2));
        }

        [Fact]
        public void EncoderOfTheBestLoopGivesTheParitiesOfTheLibrary()
        {
            // Setup.
            //a full Paranoid level: 39 data and 89 parity shards of a chunk
            var shards = Enumerable.Range(0, 39 + 89)
                .Select(i =>
                {
                    var shard = new byte[4104];
                    if (i < 39)
                        new Random(i).NextBytes(shard);
                    return shard;
                })
                .ToArray();
            var expected = shards.Select(s => (byte[])s.Clone()).ToArray();
            ReedSolomon.NET.ReedSolomon.Create(39, 89).EncodeParity(expected, 0, 4104);

            // Run.
            ReedSolomonEncoders.Get(39, 89).EncodeParity(shards, 0, 4104);

            // Assert.
            Assert.Equal(expected, shards);
        }

        [Fact]
        public void EncoderOfTheBestLoopRecoversMissingShards()
        {
            // Setup.
            var shards = Enumerable.Range(0, 39 + 89)
                .Select(i =>
                {
                    var shard = new byte[4104];
                    if (i < 39)
                        new Random(i).NextBytes(shard);
                    return shard;
                })
                .ToArray();
            var encoder = ReedSolomonEncoders.Get(39, 89);
            encoder.EncodeParity(shards, 0, 4104);
            var original = shards.Select(s => (byte[])s.Clone()).ToArray();

            //every third shard lost, data and parities
            var present = Enumerable.Range(0, shards.Length).Select(i => i % 3 != 0).ToArray();
            for (var i = 0; i < shards.Length; i++)
                if (!present[i])
                    shards[i] = new byte[4104];

            // Run.
            encoder.DecodeMissing(shards, present, 0, 4104);

            // Assert.
            Assert.Equal(original, shards);
        }
    }
}
