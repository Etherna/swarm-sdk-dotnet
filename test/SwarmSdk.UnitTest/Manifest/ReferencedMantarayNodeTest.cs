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

using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Manifest
{
    public class ReferencedMantarayNodeTest
    {
        // Tests.
        [Fact]
        public void ConstructorRejectsAChunkOfAnotherReference()
        {
            // Setup.
            var bmt = new SwarmChunkBmt();
            var data = Enumerable.Range(0, 42).Select(i => (byte)i).ToArray();
            var spanData = SwarmCac.BuildFromData(SwarmHash.Zero, data).SpanData;
            var chunk = new SwarmCac(bmt.Hash(spanData), spanData);
            SwarmReference anotherReference = "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9";
            
            // Run.
            var thrown = Assert.Throws<SwarmChunkVerificationException>(() => new ReferencedMantarayNode(
                chunk,
                anotherReference,
                new MemoryChunkStore(),
                RedundancyLevel.Paranoid,
                RedundancyStrategy.Data,
                true,
                null,
                NodeType.Edge));
            
            // Assert.
            Assert.Equal(anotherReference.Hash, thrown.Hash);
        }

        [Fact]
        public async Task DecodeFromChunkAsyncRejectsAContentLargerThanAnyNodeBeforeReadingIt()
        {
            // Setup.
            //a root of 64 full children, 32 MiB of content, with none of them in the store
            byte[] rootSpanData =
            [
                .. SwarmCac.LengthToSpan(64UL * 128 * SwarmCac.DataSize),
                .. Enumerable.Range(1, 64).SelectMany(i => Enumerable.Repeat((byte)i, SwarmHash.HashSize))
            ];
            var rootChunk = new SwarmCac(new SwarmChunkBmt().Hash(rootSpanData), rootSpanData);
            var node = new ReferencedMantarayNode(
                rootChunk,
                new SwarmReference(rootChunk.Hash, null),
                new MemoryChunkStore(),
                RedundancyLevel.None,
                RedundancyStrategy.Data,
                true,
                null,
                NodeType.Edge);

            // Run.
            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => node.DecodeFromChunkAsync());

            // Assert.
            Assert.Contains("larger than a manifest node can be", thrown.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task DecodeFromChunkAsyncRejectsATreeWhoseFirstChildIsNotWhatItsParentImplies()
        {
            // Setup.
            //a root of two children, the first of 20 bytes where a full chunk is implied
            var bmt = new SwarmChunkBmt();
            byte[] firstChildSpanData = [.. SwarmCac.LengthToSpan(20), .. new byte[20]];
            var firstChild = new SwarmCac(bmt.Hash(firstChildSpanData), firstChildSpanData);
            byte[] lastChildSpanData = [.. SwarmCac.LengthToSpan(SwarmCac.DataSize), .. new byte[SwarmCac.DataSize]];
            var lastChild = new SwarmCac(bmt.Hash(lastChildSpanData), lastChildSpanData);
            byte[] rootSpanData =
            [
                .. SwarmCac.LengthToSpan(2 * SwarmCac.DataSize),
                .. firstChild.Hash.ToByteArray(),
                .. lastChild.Hash.ToByteArray()
            ];
            var rootChunk = new SwarmCac(bmt.Hash(rootSpanData), rootSpanData);
            var chunkStore = new MemoryChunkStore();
            foreach (var chunk in new[] { rootChunk, firstChild, lastChild })
                await chunkStore.AddAsync(chunk);
            var node = new ReferencedMantarayNode(
                rootChunk,
                new SwarmReference(rootChunk.Hash, null),
                chunkStore,
                RedundancyLevel.None,
                RedundancyStrategy.Data,
                true,
                null,
                NodeType.Edge);

            // Run.
            var thrown = await Assert.ThrowsAsync<SwarmChunkFormatException>(() => node.DecodeFromChunkAsync());

            // Assert.
            Assert.Equal(firstChild.Hash, thrown.Hash);
        }
    }
}
