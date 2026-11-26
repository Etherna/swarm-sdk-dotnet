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

using ReedSolomon.NET;
using ReedSolomon.NET.Loops;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Etherna.SwarmSdk.Chunks
{
    /// <summary>
    /// The coding loop of the Reed-Solomon encoder and decoder, multiplying in GF(2^8) many bytes at a time. Each byte is
    /// split in its two nibbles, and a coefficient multiplies them through two 16-entry tables with a byte shuffle: 32
    /// bytes per instruction with AVX2, 16 with SSSE3. Without them, one byte at a time through the multiplication table
    /// of the field. The products are the ones of <see cref="Galois.MultiplicationTable"/>, so the encoder gives the
    /// same shards whatever loop it codes with
    /// </summary>
    internal sealed class SimdCodingLoop : CodingLoopBase
    {
        // Enums.
        public enum InstructionSet
        {
            Scalar,
            Ssse3,
            Avx2
        }

        // Static fields.
        private static readonly Vector128<byte>[] highNibbleProducts = BuildNibbleProducts(4);
        private static readonly Vector128<byte>[] lowNibbleProducts = BuildNibbleProducts(0);

        // Fields.
        private readonly InstructionSet instructionSet;

        // Constructor.
        /// <param name="instructionSet">The instructions to code with: they must be supported</param>
        public SimdCodingLoop(InstructionSet instructionSet)
        {
            if (instructionSet == InstructionSet.Avx2 && !Avx2.IsSupported ||
                instructionSet == InstructionSet.Ssse3 && !Ssse3.IsSupported)
                throw new PlatformNotSupportedException($"{instructionSet} is not supported");
            this.instructionSet = instructionSet;
        }

        // Static properties.
        /// <summary>
        /// The loop coding with the widest instructions the processor supports
        /// </summary>
        public static SimdCodingLoop Best { get; } = new(
            Avx2.IsSupported ? InstructionSet.Avx2 :
            Ssse3.IsSupported ? InstructionSet.Ssse3 :
            InstructionSet.Scalar);

        // Methods.
        public override void CodeSomeShards(
            byte[][] matrixRows,
            byte[][] inputs,
            in int inputCount,
            byte[][] outputs,
            in int outputCount,
            in int offset,
            in int byteCount)
        {
            ArgumentNullException.ThrowIfNull(matrixRows);
            ArgumentNullException.ThrowIfNull(inputs);
            ArgumentNullException.ThrowIfNull(outputs);

            for (var o = 0; o < outputCount; o++)
            {
                var output = outputs[o].AsSpan(offset, byteCount);
                var matrixRow = matrixRows[o];

                //an output is the sum of its inputs times their coefficients: the first one sets it, the next ones add
                for (var i = 0; i < inputCount; i++)
                    MultiplyAdd(matrixRow[i], inputs[i].AsSpan(offset, byteCount), output, i > 0);
            }
        }

        // Helpers.
        private static Vector128<byte>[] BuildNibbleProducts(int nibbleShift)
        {
            var products = new Vector128<byte>[256];
            Span<byte> coefficientProducts = stackalloc byte[16];
            for (var coefficient = 0; coefficient < 256; coefficient++)
            {
                for (var nibble = 0; nibble < 16; nibble++)
                    coefficientProducts[nibble] = Galois.MultiplicationTable[coefficient][nibble << nibbleShift];
                products[coefficient] = Vector128.Create((ReadOnlySpan<byte>)coefficientProducts);
            }
            return products;
        }

        /// <summary>
        /// Multiply the input by the coefficient in GF(2^8), adding the product to the output or setting it
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MultiplyAdd(byte coefficient, ReadOnlySpan<byte> input, Span<byte> output, bool add)
        {
            ref var inputRef = ref MemoryMarshal.GetReference(input);
            ref var outputRef = ref MemoryMarshal.GetReference(output);
            var length = (nuint)input.Length;
            nuint i = 0;

            if (instructionSet == InstructionSet.Avx2)
            {
                var lowProducts = Vector256.Create(lowNibbleProducts[coefficient], lowNibbleProducts[coefficient]);
                var highProducts = Vector256.Create(highNibbleProducts[coefficient], highNibbleProducts[coefficient]);
                var nibbleMask = Vector256.Create((byte)0x0F);
                for (; i + 32 <= length; i += 32)
                {
                    var bytes = Vector256.LoadUnsafe(ref inputRef, i);
                    var product =
                        Avx2.Shuffle(lowProducts, bytes & nibbleMask) ^
                        Avx2.Shuffle(highProducts, Vector256.ShiftRightLogical(bytes.AsUInt16(), 4).AsByte() & nibbleMask);
                    if (add)
                        product ^= Vector256.LoadUnsafe(ref outputRef, i);
                    product.StoreUnsafe(ref outputRef, i);
                }
            }

            if (instructionSet != InstructionSet.Scalar)
            {
                var lowProducts = lowNibbleProducts[coefficient];
                var highProducts = highNibbleProducts[coefficient];
                var nibbleMask = Vector128.Create((byte)0x0F);
                for (; i + 16 <= length; i += 16)
                {
                    var bytes = Vector128.LoadUnsafe(ref inputRef, i);
                    var product =
                        Ssse3.Shuffle(lowProducts, bytes & nibbleMask) ^
                        Ssse3.Shuffle(highProducts, Vector128.ShiftRightLogical(bytes.AsUInt16(), 4).AsByte() & nibbleMask);
                    if (add)
                        product ^= Vector128.LoadUnsafe(ref outputRef, i);
                    product.StoreUnsafe(ref outputRef, i);
                }
            }

            //the bytes past the last full vector, or every byte without vector instructions
            var coefficientProducts = Galois.MultiplicationTable[coefficient];
            for (var b = (int)i; b < input.Length; b++)
                output[b] = add
                    ? (byte)(output[b] ^ coefficientProducts[input[b]])
                    : coefficientProducts[input[b]];
        }
    }
}
