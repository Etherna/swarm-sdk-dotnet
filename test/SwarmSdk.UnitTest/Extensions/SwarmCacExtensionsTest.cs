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

using Etherna.SwarmSdk.Chunks;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Extensions
{
    public class SwarmCacExtensionsTest
    {
        // Data.
        public static TheoryData<RedundancyLevel, bool, int> PipelineContents()
        {
            //every level, plain and encrypted, from an empty content to a tree of three levels
            var contents = new TheoryData<RedundancyLevel, bool, int>();
            foreach (var level in Enum.GetValues<RedundancyLevel>())
                foreach (var encrypted in new[] { false, true })
                    foreach (var size in new[] { 0, 1, SwarmCac.DataSize, SwarmCac.DataSize + 1, SwarmCac.DataSize * 128, SwarmCac.DataSize * 128 + 1 })
                        contents.Add(level, encrypted, size);
            return contents;
        }

        // Tests.
        /// <summary>
        /// Every chunk the pipeline writes decodes under its reference, with the data its span declares or the references
        /// its span implies, and the content reads back through the decode
        /// </summary>
        [Theory]
        [MemberData(nameof(PipelineContents))]
        public async Task DecodeAcceptsEveryChunkThePipelineWrites(RedundancyLevel level, bool encrypted, int size)
        {
            // Setup.
            var data = new byte[size];
            new Random(size).NextBytes(data);
            var chunkStore = new MemoryChunkStore();
            SwarmReference rootReference;
            using (var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                       chunkStore, new FakePostageStamper(), level, encrypted, 0, null))
            {
                await using var dataStream = new MemoryStream(data);
                rootReference = await pipeline.HashDataAsync(dataStream);
            }
            var hasher = new Hasher();
            var decodedChunks = 0;
            var notFoundReferences = 0;

            // Run.
            await new ChunkTraverser(chunkStore).TraverseAsync(
                rootReference,
                (chunk, shardReference) =>
                {
                    var decoded = ((SwarmCac)chunk).Decode(shardReference.Reference, hasher);
                    decodedChunks++;

                    Assert.Equal(encrypted, decoded.IsEncrypted);
                    if (decoded is SwarmDecodedDataCac dataChunk)
                        Assert.Equal((int)decoded.SpanLength, dataChunk.Data.Length);
                    else
                    {
                        var intermediateChunk = (SwarmDecodedIntermediateCac)decoded;
                        Assert.True(SwarmCac.TryCountIntermediateReferences(
                            decoded.SpanLength, decoded.RedundancyLevel, encrypted, out var dataShards, out var parityShards));
                        Assert.Equal(dataShards + parityShards, intermediateChunk.ChildReferences.Count);
                        Assert.Equal(parityShards, intermediateChunk.Parities);
                    }
                    return Task.CompletedTask;
                },
                null,
                _ => { notFoundReferences++; return Task.CompletedTask; },
                level,
                RedundancyStrategy.Data,
                false);
            await using var contentStream = await ChunkDataStream.BuildNewAsync(
                rootReference, chunkStore, level, RedundancyStrategy.Data, false);
            var readData = await contentStream.ToByteArrayAsync();

            // Assert.
            Assert.True(decodedChunks > 0);
            Assert.Equal(0, notFoundReferences);
            Assert.Equal(data, readData);
        }

        [Fact]
        public void DecodeRejectsAChunkOfAnotherReference()
        {
            // Setup.
            var bmt = new SwarmChunkBmt();
            var data = Enumerable.Range(0, 42).Select(i => (byte)i).ToArray();
            var spanData = SwarmCac.BuildFromData(SwarmHash.Zero, data).SpanData;
            var chunk = new SwarmCac(bmt.Hash(spanData), spanData);
            SwarmReference anotherReference = "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9";
            
            // Run.
            var thrown = Assert.Throws<SwarmChunkVerificationException>(() => chunk.Decode(anotherReference, new Hasher()));
            
            // Assert.
            Assert.Equal(anotherReference.Hash, thrown.Hash);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DecodeRejectsAChunkWhoseSpanDeclaresAnUndefinedRedundancyLevel(bool encrypted)
        {
            // Setup.
            //an encoded span, its last byte over 128, declaring two chunks of content at the level 127
            var span = new byte[SwarmCac.SpanSize];
            BinaryPrimitives.WriteUInt64LittleEndian(span, 2 * SwarmCac.DataSize);
            span[^1] = 0xFF;
            var data = new byte[2 * (encrypted ? SwarmReference.EncryptedSize : SwarmReference.PlainSize)];
            var (chunk, reference) = BuildChunk(span, data, encrypted);

            // Run.
            var thrown = Assert.Throws<SwarmChunkFormatException>(() => chunk.Decode(reference, new Hasher()));

            // Assert.
            Assert.Equal(chunk.Hash, thrown.Hash);
            Assert.Contains("redundancy level 127", thrown.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void DecodeRejectsAChunkWhoseSpanNoTrieCanHold()
        {
            // Setup.
            //arbitrary bytes read as a chunk: the first eight are a span of about 2^62 bytes
            var bmt = new SwarmChunkBmt();
            var spanData = Encoding.UTF8.GetBytes("second update of the feed, written as plain text");
            var chunk = new SwarmCac(bmt.Hash(spanData), spanData);

            // Run.
            var thrown = Assert.Throws<SwarmChunkFormatException>(() =>
                chunk.Decode(new SwarmReference(chunk.Hash, null), new Hasher()));

            // Assert.
            Assert.Equal(chunk.Hash, thrown.Hash);
            Assert.Contains("exceeds the content a chunk trie can hold", thrown.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(100, 20)]
        [InlineData(20, 100)]
        public void DecodeRejectsADataChunkNotHoldingTheDataOfItsSpan(int spanLength, int dataLength)
        {
            // Setup.
            var span = new byte[SwarmCac.SpanSize];
            BinaryPrimitives.WriteUInt64LittleEndian(span, (ulong)spanLength);
            var (chunk, reference) = BuildChunk(span, new byte[dataLength], false);

            // Run.
            var thrown = Assert.Throws<SwarmChunkFormatException>(() => chunk.Decode(reference, new Hasher()));

            // Assert.
            Assert.Equal(chunk.Hash, thrown.Hash);
            Assert.Contains($"data of {dataLength} bytes", thrown.Message, StringComparison.Ordinal);
            Assert.Contains($"span of {spanLength} bytes", thrown.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void DecodeRejectsAnEncryptedChunkNotFillingTheChunk()
        {
            // Setup.
            //an encrypted data chunk cut after its first 100 bytes of data: its span still decrypts to 20 bytes
            var span = new byte[SwarmCac.SpanSize];
            BinaryPrimitives.WriteUInt64LittleEndian(span, 20);
            var (fullChunk, fullReference) = BuildChunk(span, new byte[20], true);
            var spanData = fullChunk.SpanData[..(SwarmCac.SpanSize + 100)];
            var chunk = new SwarmCac(new SwarmChunkBmt().Hash(spanData), spanData);
            var reference = new SwarmReference(chunk.Hash, fullReference.EncryptionKey);

            // Run.
            var thrown = Assert.Throws<SwarmChunkFormatException>(() => chunk.Decode(reference, new Hasher()));

            // Assert.
            Assert.Equal(chunk.Hash, thrown.Hash);
            Assert.Contains("data of 100 bytes", thrown.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void DecodeRejectsAnIntermediateChunkNotHoldingTheReferencesOfItsSpan()
        {
            // Setup.
            //a span of two data chunks over a payload of three references
            var bmt = new SwarmChunkBmt();
            var spanData = new byte[SwarmCac.SpanSize + 3 * SwarmReference.PlainSize];
            BinaryPrimitives.WriteUInt64LittleEndian(spanData, SwarmCac.DataSize + 1);
            var chunk = new SwarmCac(bmt.Hash(spanData), spanData);

            // Run.
            var thrown = Assert.Throws<SwarmChunkFormatException>(() =>
                chunk.Decode(new SwarmReference(chunk.Hash, null), new Hasher()));

            // Assert.
            Assert.Equal(chunk.Hash, thrown.Hash);
            Assert.Contains("96 bytes", thrown.Message, StringComparison.Ordinal);
            Assert.Contains("2 data", thrown.Message, StringComparison.Ordinal);
        }

        // Helpers.
        private static (SwarmCac Chunk, SwarmReference Reference) BuildChunk(byte[] span, byte[] data, bool encrypted)
        {
            EncryptionKey256? key = null;
            byte[] spanData = [.. span, .. data];
            if (encrypted)
                key = ChunkEncrypter.EncryptChunk(span, data, null, new Hasher(), out spanData);
            var chunk = new SwarmCac(new SwarmChunkBmt().Hash(spanData), spanData);
            return (chunk, new SwarmReference(chunk.Hash, key));
        }
    }
}
