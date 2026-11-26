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

using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Manifest
{
    public class ReferencedMantarayManifestTest
    {
        // Tests.
        [Fact]
        public async Task ConcurrentLookupsOfOneManifestFetchEachNodeOnce()
        {
            // Setup.
            //a video as a manifest holds it: two renditions of ten segments, two thumbnails, a caption
            var paths = Enumerable.Range(0, 10)
                .SelectMany(i => new[] { $"hls/1080p/segment-{i}.ts", $"hls/720p/segment-{i}.ts" })
                .Concat(["captions/en.vtt", "thumbnails/large.jpg", "thumbnails/small.jpg"])
                .ToArray();
            var manifestChunkStore = new MemoryChunkStore();
            var writableManifest = new WritableMantarayManifest(
                manifestChunkStore, new FakePostageStamper(), RedundancyLevel.None, false, 0, null);
            var hasher = new Hasher();
            var references = paths.ToDictionary(
                path => path,
                path => new SwarmReference(hasher.ComputeHash(path), null));
            foreach (var path in paths)
                writableManifest.Add(path, ManifestEntry.NewFile(references[path], new Dictionary<string, string>()));
            var rootReference = await writableManifest.GetReferenceAsync(new Hasher());

            //a store counting the reads of each chunk, which answers none before every lookup has asked for its first
            var reads = new ConcurrentDictionary<SwarmHash, int>();
            var everyLookupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var chunkStoreMock = new Mock<IReadOnlyChunkStore>();
            chunkStoreMock
                .Setup(s => s.GetAsync(It.IsAny<SwarmHash>(), It.IsAny<CancellationToken>()))
                .Returns<SwarmHash, CancellationToken>(async (hash, cancellationToken) =>
                {
                    reads.AddOrUpdate(hash, 1, (_, count) => count + 1);
                    await everyLookupStarted.Task.WaitAsync(cancellationToken);
                    return await manifestChunkStore.GetAsync(hash, cancellationToken);
                });
            var manifest = ReferencedMantarayManifest.BuildNew(
                rootReference, chunkStoreMock.Object, RedundancyLevel.None);

            // Run.
            var lookups = paths
                .Select(path => manifest.GetResourceInfoAsync(path, ManifestPathResolver.IdentityResolver))
                .ToArray();
            everyLookupStarted.SetResult();
            var results = await Task.WhenAll(lookups).WaitAsync(TimeSpan.FromSeconds(10));

            // Assert.
            Assert.Equal(paths.Select(path => references[path]), results.Select(result => result.Result.Reference));
            Assert.All(reads, read => Assert.Equal(1, read.Value));
            Assert.True(reads.Count > 1);
        }
    }
}
