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
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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

        [Fact]
        public async Task DecodeFromChunkAsyncWaitsForTheVisitInFlight()
        {
            // Setup.
            var (node, chunkStoreMock, firstRead, nodeChunk) = await BuildNodeWithAPendingFirstReadAsync();

            // Run.
            var visit = node.OnVisitingAsync();
            var decode = node.DecodeFromChunkAsync();
            firstRead.SetResult(nodeChunk);
            await Task.WhenAll(visit, decode).WaitAsync(TimeSpan.FromSeconds(10));

            // Assert.
            Assert.True(node.IsDecoded);
            chunkStoreMock.Verify(s => s.GetAsync(node.Reference.Hash, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task FetchChunkAsyncFetchesTheChunkOnceForTheFetchesAndVisitsInFlight()
        {
            // Setup.
            var (node, chunkStoreMock, firstRead, nodeChunk) = await BuildNodeWithAPendingFirstReadAsync();

            // Run.
            Task[] steps = [node.FetchChunkAsync(), node.FetchChunkAsync(), node.OnVisitingAsync()];
            firstRead.SetResult(nodeChunk);
            await Task.WhenAll(steps).WaitAsync(TimeSpan.FromSeconds(10));
            await node.FetchChunkAsync();

            // Assert.
            Assert.Equal(nodeChunk, node.Chunk);
            Assert.True(node.IsDecoded);
            chunkStoreMock.Verify(s => s.GetAsync(node.Reference.Hash, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task OnVisitingAsyncFetchesTheNodeOnceForTheVisitsInFlight()
        {
            // Setup.
            var (node, chunkStoreMock, firstRead, nodeChunk) = await BuildNodeWithAPendingFirstReadAsync();

            // Run.
            Task[] visits = [node.OnVisitingAsync(), node.OnVisitingAsync(), node.OnVisitingAsync()];
            firstRead.SetResult(nodeChunk);
            await Task.WhenAll(visits).WaitAsync(TimeSpan.FromSeconds(10));

            // Assert.
            Assert.True(node.IsDecoded);
            Assert.Single(node.Forks);
            chunkStoreMock.Verify(s => s.GetAsync(node.Reference.Hash, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task OnVisitingAsyncLeavesTheNodeToTheNextVisitWhenTheOneInFlightFails()
        {
            // Setup.
            var (node, chunkStoreMock, firstRead, _) = await BuildNodeWithAPendingFirstReadAsync();

            // Run.
            var failedVisit = node.OnVisitingAsync();
            var nextVisit = node.OnVisitingAsync();
            firstRead.SetException(new IOException("The read failed"));

            // Assert.
            await Assert.ThrowsAsync<IOException>(() => failedVisit.WaitAsync(TimeSpan.FromSeconds(10)));
            await nextVisit.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(node.IsDecoded);
            chunkStoreMock.Verify(
                s => s.GetAsync(node.Reference.Hash, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task OnVisitingAsyncLeavesTheNodeToTheNextVisitWhenTheOneInFlightIsCancelled()
        {
            // Setup.
            var (node, chunkStoreMock, _, _) = await BuildNodeWithAPendingFirstReadAsync();
            using var cancellationTokenSource = new CancellationTokenSource();

            // Run.
            var cancelledVisit = node.OnVisitingAsync(cancellationTokenSource.Token);
            var nextVisit = node.OnVisitingAsync();
            await cancellationTokenSource.CancelAsync();

            // Assert.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => cancelledVisit.WaitAsync(TimeSpan.FromSeconds(10)));
            await nextVisit.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(node.IsDecoded);
            chunkStoreMock.Verify(
                s => s.GetAsync(node.Reference.Hash, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task OnVisitingAsyncLeavesTheVisitInFlightWhenAWaitingOneIsCancelled()
        {
            // Setup.
            var (node, chunkStoreMock, firstRead, nodeChunk) = await BuildNodeWithAPendingFirstReadAsync();
            using var cancellationTokenSource = new CancellationTokenSource();

            // Run.
            var visitInFlight = node.OnVisitingAsync();
            var waitingVisit = node.OnVisitingAsync(cancellationTokenSource.Token);
            await cancellationTokenSource.CancelAsync();

            // Assert.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => waitingVisit.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(node.IsDecoded);

            firstRead.SetResult(nodeChunk);
            await visitInFlight.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(node.IsDecoded);
            chunkStoreMock.Verify(s => s.GetAsync(node.Reference.Hash, It.IsAny<CancellationToken>()), Times.Once);
        }

        // Helpers.
        /// <summary>
        /// A node not fetched yet, over a store whose first read of its chunk pends until the test ends it, or until
        /// the token that read received is cancelled. The reads after the first answer the chunk
        /// </summary>
        private static async Task<(
            ReferencedMantarayNode Node,
            Mock<IReadOnlyChunkStore> ChunkStoreMock,
            TaskCompletionSource<SwarmChunk> FirstRead,
            SwarmChunk NodeChunk)> BuildNodeWithAPendingFirstReadAsync()
        {
            var manifestChunkStore = new MemoryChunkStore();
            var manifest = new WritableMantarayManifest(
                manifestChunkStore, new FakePostageStamper(), RedundancyLevel.None, false, 0, null);
            manifest.Add(
                "index.html",
                ManifestEntry.NewFile(
                    "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9",
                    new Dictionary<string, string>()));
            var reference = await manifest.GetReferenceAsync(new Hasher());
            var nodeChunk = await manifestChunkStore.GetAsync(reference.Hash);

            var firstRead = new TaskCompletionSource<SwarmChunk>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reads = 0;
            var chunkStoreMock = new Mock<IReadOnlyChunkStore>();
            chunkStoreMock
                .Setup(s => s.GetAsync(reference.Hash, It.IsAny<CancellationToken>()))
                .Returns<SwarmHash, CancellationToken>((_, cancellationToken) =>
                    Interlocked.Increment(ref reads) == 1 ?
                        firstRead.Task.WaitAsync(cancellationToken) :
                        Task.FromResult(nodeChunk));

            var node = new ReferencedMantarayNode(
                reference,
                chunkStoreMock.Object,
                RedundancyLevel.None,
                RedundancyStrategy.Data,
                true,
                null,
                NodeType.Edge);
            return (node, chunkStoreMock, firstRead, nodeChunk);
        }
    }
}
