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
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Manifest
{
    public class SwarmAddressResolverTest
    {
        // Internal classes.
        private sealed record TestManifestData(
            IChunkStore ChunkStore,
            SwarmReference RootReference,
            IReadOnlyDictionary<string, ManifestEntry> Entries);

        // Data.
        public static TheoryData<Func<SwarmReference, IReadOnlyChunkStore, Task>> ResolutionsAtLevelNone => new()
        {
            (rootReference, chunkStore) => SwarmAddressResolver.ResolveReferenceAsync(
                new SwarmAddress(rootReference, "index.html"),
                chunkStore,
                RedundancyLevel.None),
            (rootReference, chunkStore) => SwarmAddressResolver.ResolveReferenceAsync(
                $"{rootReference}/index.html",
                chunkStore,
                RedundancyLevel.None),
            (rootReference, chunkStore) => SwarmAddressResolver.TryGetFileNameAsync(
                new SwarmAddress(rootReference, "index.html"),
                chunkStore,
                RedundancyLevel.None)
        };

        private static readonly (string Path, string ContentType, string? Filename, int SizeKb)[] TestFiles =
        [
            ("assets/app.js", "application/javascript", "app.js", 8),
            ("data.bin", "application/octet-stream", null, 2),
            ("index.html", "text/html", "index.html", 4)
        ];

        // Tests.
        [Theory]
        [MemberData(nameof(ResolutionsAtLevelNone))]
        public async Task ResolutionAtLevelNone_AsksForAMissingForkNodeOnce_AndForNoReplica(
            Func<SwarmReference, IReadOnlyChunkStore, Task> resolution)
        {
            // Setup.
            var manifestData = await BuildTestManifestAsync();
            var forkNodeHash = await GetIndexForkNodeHashAsync(manifestData);

            //a store without the fork node, recording what it is asked for
            var requestedHashes = new ConcurrentQueue<SwarmHash>();
            var chunkStoreMock = new Mock<IReadOnlyChunkStore>();
            chunkStoreMock
                .Setup(s => s.GetAsync(It.IsAny<SwarmHash>(), It.IsAny<CancellationToken>()))
                .Returns<SwarmHash, CancellationToken>((hash, cancellationToken) =>
                {
                    requestedHashes.Enqueue(hash);
                    return hash == forkNodeHash ?
                        Task.FromException<SwarmChunk>(new KeyNotFoundException()) :
                        manifestData.ChunkStore.GetAsync(hash, cancellationToken);
                });
            SwarmHash[] expectedHashes = [manifestData.RootReference.Hash, forkNodeHash];

            // Run.
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => resolution(manifestData.RootReference, chunkStoreMock.Object));

            // Assert.
            Assert.Equal(expectedHashes, requestedHashes);
        }

        [Fact]
        public async Task ResolveReferenceAsync_FromAddress_ResolvesThroughManifest()
        {
            // Setup.
            var manifestData = await BuildTestManifestAsync();
            var address = new SwarmAddress(manifestData.RootReference, "index.html");

            // Run.
            var reference = await SwarmAddressResolver.ResolveReferenceAsync(
                address,
                manifestData.ChunkStore);

            // Assert.
            Assert.Equal(manifestData.Entries["index.html"].Reference, reference);
        }

        [Fact]
        public async Task ResolveReferenceAsync_FromAddressString_ResolvesThroughManifest()
        {
            // Setup.
            var manifestData = await BuildTestManifestAsync();
            var addressString = $"{manifestData.RootReference}/assets/app.js";

            // Run.
            var reference = await SwarmAddressResolver.ResolveReferenceAsync(
                addressString,
                manifestData.ChunkStore);

            // Assert.
            Assert.Equal(manifestData.Entries["assets/app.js"].Reference, reference);
        }

        [Fact]
        public async Task ResolveReferenceAsync_FromHashString_DoesntAccessChunkStore()
        {
            // Setup.
            const string hashString = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            var chunkStoreMock = new Mock<IReadOnlyChunkStore>(MockBehavior.Strict);

            // Run.
            var reference = await SwarmAddressResolver.ResolveReferenceAsync(
                hashString,
                chunkStoreMock.Object);

            // Assert.
            Assert.Equal(new SwarmReference(SwarmHash.FromString(hashString), null), reference);
        }

        [Fact]
        public async Task ResolveReferenceAsync_WithARedundancyLevel_ResolvesAForkNodeFromItsReplicas()
        {
            // Setup.
            var manifestData = await BuildTestManifestAsync();
            var forkNodeHash = await GetIndexForkNodeHashAsync(manifestData);

            //leave only the replicas of the fork node in the store
            var forkNodeChunk = (SwarmCac)await manifestData.ChunkStore.GetAsync(forkNodeHash);
            var replicator = new ChunkReplicator(
                RedundancyLevel.Medium,
                manifestData.ChunkStore,
                new FakePostageStamper(),
                new PrivateKeySigner(SwarmSoc.ReplicasOwnerPrivateKey));
            await replicator.AddChunkReplicasAsync(forkNodeChunk, new Hasher());
            await manifestData.ChunkStore.RemoveAsync(forkNodeHash);

            // Run.
            var reference = await SwarmAddressResolver.ResolveReferenceAsync(
                new SwarmAddress(manifestData.RootReference, "index.html"),
                manifestData.ChunkStore,
                RedundancyLevel.Medium);

            // Assert.
            Assert.Equal(manifestData.Entries["index.html"].Reference, reference);
        }

        [Fact]
        public async Task ResolveResourceInfoAsync_CancelledWhileAForkNodeIsFetched_ThrowsTheCancellation()
        {
            // Setup.
            var manifestData = await BuildTestManifestAsync();
            var forkNodeHash = await GetIndexForkNodeHashAsync(manifestData);

            //a store honouring the token it is given, whatever the token source of the caller says
            using var cancellationTokenSource = new CancellationTokenSource();
            var chunkStoreMock = new Mock<IReadOnlyChunkStore>();
            chunkStoreMock
                .Setup(s => s.GetAsync(It.IsAny<SwarmHash>(), It.IsAny<CancellationToken>()))
                .Returns<SwarmHash, CancellationToken>((hash, cancellationToken) =>
                {
                    if (hash == forkNodeHash)
                        cancellationTokenSource.Cancel();
                    return cancellationToken.IsCancellationRequested ?
                        Task.FromCanceled<SwarmChunk>(cancellationToken) :
                        manifestData.ChunkStore.GetAsync(hash, cancellationToken);
                });

            // Run & Assert.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => SwarmAddressResolver.ResolveResourceInfoAsync(
                    new SwarmAddress(manifestData.RootReference, "index.html"),
                    chunkStoreMock.Object,
                    ManifestPathResolver.IdentityResolver,
                    RedundancyLevel.None,
                    cancellationToken: cancellationTokenSource.Token));
        }

        [Fact]
        public async Task ResolveResourceInfoAsync_ResolvesPathToReferenceAndMetadata()
        {
            // Setup.
            var manifestData = await BuildTestManifestAsync();

            foreach (var (path, entry) in manifestData.Entries)
            {
                var address = new SwarmAddress(manifestData.RootReference, path);

                // Run.
                var result = await SwarmAddressResolver.ResolveResourceInfoAsync(
                    address,
                    manifestData.ChunkStore,
                    ManifestPathResolver.IdentityResolver,
                    RedundancyLevel.None);

                // Assert.
                Assert.Equal(entry.Reference, result.Result.Reference);
                Assert.Equal(
                    entry.Metadata[ManifestEntry.ContentTypeKey],
                    result.Result.Metadata[ManifestEntry.ContentTypeKey]);
            }
        }

        [Fact]
        public async Task TryGetFileNameAsync_ReturnsFilenameMetadata()
        {
            // Setup.
            var manifestData = await BuildTestManifestAsync();
            var address = new SwarmAddress(manifestData.RootReference, "index.html");

            // Run.
            var fileName = await SwarmAddressResolver.TryGetFileNameAsync(
                address,
                manifestData.ChunkStore);

            // Assert.
            Assert.Equal("index.html", fileName);
        }

        [Fact]
        public async Task TryGetFileNameAsync_ReturnsNullWithoutFilenameMetadata()
        {
            // Setup.
            var manifestData = await BuildTestManifestAsync();
            var address = new SwarmAddress(manifestData.RootReference, "data.bin");

            // Run.
            var fileName = await SwarmAddressResolver.TryGetFileNameAsync(
                address,
                manifestData.ChunkStore);

            // Assert.
            Assert.Null(fileName);
        }

        // Helpers.
        private static async Task<TestManifestData> BuildTestManifestAsync()
        {
            var chunkStore = new MemoryChunkStore();
            var postageStamper = new FakePostageStamper();
            var manifest = new WritableMantarayManifest(
                chunkStore,
                postageStamper,
                RedundancyLevel.None,
                false,
                0,
                null);

            var entries = new Dictionary<string, ManifestEntry>();
            var fileDataRand = new Random(0);

            foreach (var (path, contentType, filename, sizeKb) in TestFiles)
            {
                var fileData = new byte[sizeKb * 1024];
                fileDataRand.NextBytes(fileData);

                var hasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                    chunkStore,
                    postageStamper,
                    RedundancyLevel.None,
                    false,
                    0,
                    null);
                var fileReference = await hasherPipeline.HashDataAsync(fileData);

                var metadata = new Dictionary<string, string>
                {
                    [ManifestEntry.ContentTypeKey] = contentType
                };
                if (filename is not null)
                    metadata[ManifestEntry.FilenameKey] = filename;

                var entry = ManifestEntry.NewFile(fileReference, metadata);

                manifest.Add(path, entry);
                entries[path] = entry;
            }

            var rootReference = await manifest.GetReferenceAsync(new Hasher());

            return new TestManifestData(chunkStore, rootReference, entries);
        }

        /// <summary>
        /// The hash of the node the root of the test manifest forks to for "index.html"
        /// </summary>
        private static async Task<SwarmHash> GetIndexForkNodeHashAsync(TestManifestData manifestData)
        {
            var rootNode = new ReferencedMantarayNode(
                manifestData.RootReference,
                manifestData.ChunkStore,
                RedundancyLevel.None,
                RedundancyStrategy.Data,
                true,
                null,
                NodeType.Edge);
            await rootNode.OnVisitingAsync();
            return rootNode.Forks['i'].Node.Reference.Hash;
        }
    }
}
