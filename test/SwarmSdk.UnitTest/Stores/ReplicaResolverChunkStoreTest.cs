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
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Stores
{
    public class ReplicaResolverChunkStoreTest
    {
        // Internal classes.
        public record DoReplicaRequestsWithDelaysTestElement(
            SwarmHash OriginalHash,
            RedundancyLevel RedundancyLevel,
            SwarmHash[][] RequestsByLevel);

        // Data.
        public static IEnumerable<object[]> DoReplicaRequestsWithDelaysTests
        {
            get
            {
                var tests = new List<DoReplicaRequestsWithDelaysTestElement>
                {
                    new("1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9",
                        RedundancyLevel.None,
                        [["1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9"]]),

                    new("1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9",
                        RedundancyLevel.Medium,
                        [
                            ["1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9"],
                            [
                                "5893094fe24d67615f54acab58e31b90cb9ed9fcb49852dee775d78533fde1f7",
                                "f173f977624b99d23ab7a04a7f2ed441749b0b7b0968cd82b9ae0b75782bfe99"
                            ]
                        ]),
                    
                    new("1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9",
                        RedundancyLevel.Strong,
                        [
                            ["1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9"],
                            [
                                "5893094fe24d67615f54acab58e31b90cb9ed9fcb49852dee775d78533fde1f7",
                                "f173f977624b99d23ab7a04a7f2ed441749b0b7b0968cd82b9ae0b75782bfe99"
                            ],
                            [
                                "8e503d17910542560cff5c6b0f34e7d0b4c6c595ef8a095f3d6fa660c196e0be",
                                "2f682452076ab5b7c4daaa52849afee7abf80c68aec29026ca0ae3bae84cae10"
                            ]
                        ]),
                    
                    new("1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9",
                        RedundancyLevel.Insane,
                        [
                            ["1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9"],
                            [
                                "5893094fe24d67615f54acab58e31b90cb9ed9fcb49852dee775d78533fde1f7",
                                "f173f977624b99d23ab7a04a7f2ed441749b0b7b0968cd82b9ae0b75782bfe99"
                            ],
                            [
                                "8e503d17910542560cff5c6b0f34e7d0b4c6c595ef8a095f3d6fa660c196e0be",
                                "2f682452076ab5b7c4daaa52849afee7abf80c68aec29026ca0ae3bae84cae10"
                            ],
                            [
                                "a50928be99eb4eb9d7c8bb8da6d25f372f09add849d40382583c258541a85867",
                                "6e2b18231e137b5dbc149e7527c330f4c1cf572ce9d74e30041abb39ff53937c",
                                "1c42ec2761dc42d9665fa450c14ea4b7e0057116976386932079a76c5f179bc3",
                                "d2360303a95ba40b322b28c350be90af186b05ef15d136a7c924074373f8094e"
                            ]
                        ]),
                    
                    new("1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9",
                        RedundancyLevel.Paranoid,
                        [
                            ["1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9"],
                            [
                                "5893094fe24d67615f54acab58e31b90cb9ed9fcb49852dee775d78533fde1f7",
                                "f173f977624b99d23ab7a04a7f2ed441749b0b7b0968cd82b9ae0b75782bfe99"
                            ],
                            [
                                "8e503d17910542560cff5c6b0f34e7d0b4c6c595ef8a095f3d6fa660c196e0be",
                                "2f682452076ab5b7c4daaa52849afee7abf80c68aec29026ca0ae3bae84cae10"
                            ],
                            [
                                "a50928be99eb4eb9d7c8bb8da6d25f372f09add849d40382583c258541a85867",
                                "6e2b18231e137b5dbc149e7527c330f4c1cf572ce9d74e30041abb39ff53937c",
                                "1c42ec2761dc42d9665fa450c14ea4b7e0057116976386932079a76c5f179bc3",
                                "d2360303a95ba40b322b28c350be90af186b05ef15d136a7c924074373f8094e"
                            ],
                            [
                                "713cfde16990678b486e0f97473e869631209bb7220d5d987c51c2b47c6babcb",
                                "35245d5f72cf3d20bac1a57839be9ab67e8250a9eff81e44327c3c2926bedb41",
                                "c43863f6a93a19eda90a6e4b92e700dd8232f1331335d9246e5144c87a4a2b31",
                                "b8218b7407a1d9fc7028d651259a7a711464d66844d873b85a9a8fd22b4cea15",
                                "0ffcdd27b42c9289f49e79169e5ca8664bfa47a1e532648fd56bf663e004155c",
                                "e112e46b12775779aa3bd85433a3afb7c2aa5d6ace7a7a7bf799d8baebc6a5af",
                                "9938739be8aa6492e25cb89d87f7b1982534a42cad8a8a1a5feca5e9ba236e3f",
                                "40d57b9125957ab33f9d902bd84554b2e528774f9fec0594663054999ea67055"
                            ]
                        ])
                };

                return tests.Select(t => new object[] { t });
            }
        }
        
        // Tests.
        [Theory, MemberData(nameof(DoReplicaRequestsWithDelaysTests))]
        public async Task DoReplicaRequestsWithDelays(DoReplicaRequestsWithDelaysTestElement test)
        {
            // Setup.
            // A virtual clock drives the level delays, so the staggered dispatch is observed
            // deterministically by advancing time level by level, without depending on wall-clock timing.
            var levelDelay = TimeSpan.FromSeconds(1);
            var fakeTimeProvider = new FakeTimeProvider();

            ConcurrentBag<SwarmHash> currentRequestsBatch = [];
            List<SwarmHash[]> requestsBatches = [];
            
            var sourceChunkStoreMock = new Mock<IChunkStore>();
            sourceChunkStoreMock.Setup(s => s.GetAsync(It.IsAny<SwarmHash>(), It.IsAny<CancellationToken>()))
                .Returns<SwarmHash, CancellationToken>(async (h, _) =>
                {
                    currentRequestsBatch.Add(h);
                    await Task.Yield();
                    throw new KeyNotFoundException();
                });
            
            var replicaChunkStore = new ReplicaResolverChunkStore(
                sourceChunkStoreMock.Object,
                test.RedundancyLevel,
                new Hasher(),
                levelDelay,
                fakeTimeProvider);

            // Run.
            // Start the resolver without awaiting: it won't complete until all level delays elapse,
            // which only happens as we advance the fake clock below.
            var getTask = Assert.ThrowsAsync<KeyNotFoundException>(() => replicaChunkStore.GetAsync(test.OriginalHash));

            for (var i = 0; i < test.RequestsByLevel.Length; i++)
            {
                // Level 0 (original hash) is dispatched immediately; each further level after one more delay.
                if (i > 0)
                    fakeTimeProvider.Advance(levelDelay);

                // Wait until the expected number of requests for the current level has been recorded.
                // The fake clock guarantees no later level can fire early, so this only settles the
                // already-triggered async continuations; it never merges levels together.
                var timeout = Stopwatch.StartNew();
                while (currentRequestsBatch.Count < test.RequestsByLevel[i].Length && timeout.Elapsed < TimeSpan.FromSeconds(5))
                    await Task.Delay(1);
                Assert.Equal(test.RequestsByLevel[i].Length, currentRequestsBatch.Count);
                
                requestsBatches.Add([..currentRequestsBatch]);
                currentRequestsBatch.Clear();
            }

            await getTask;

            // Assert.
            Assert.Equal(test.RequestsByLevel.Length, requestsBatches.Count);
            for (int i = 0; i < requestsBatches.Count; i++)
                Assert.Equal(test.RequestsByLevel[i].Order(), requestsBatches[i].Order());
            Assert.Empty(currentRequestsBatch); // no requests dispatched beyond the expected levels
        }

        [Fact]
        public async Task CanFindOriginalChunk()
        {
            // Setup.
            var originalHash = "662d90f2aa5d1c2194573f761861e264e9e43c2cc26695b03dbdeb05f022e576";
            var originalChunk = SwarmCac.BuildFromData(
                originalHash,
                Enumerable.Range(0, 100).Select(i => (byte)i).ToArray());
            
            var sourceChunkStore = new MemoryChunkStore();
            await sourceChunkStore.AddAsync(originalChunk);
            var chunkStore = new ReplicaResolverChunkStore(sourceChunkStore, RedundancyLevel.Paranoid, new Hasher());
                
            // Run.
            var chunkResult = await chunkStore.GetAsync(originalHash);
            
            // Assert.
            Assert.Equal(originalChunk, chunkResult);
        }

        [Fact]
        public async Task CanFindReplicaChunk()
        {
            // Setup.
            var originalHash = "662d90f2aa5d1c2194573f761861e264e9e43c2cc26695b03dbdeb05f022e576";
            var originalChunk = new SwarmCac(
                originalHash,
                Enumerable.Range(0, 100).Select(i => (byte)i).ToArray());
            
            var replicaSoc = new SwarmSoc(
                "2b2d90f2aa5d1c2194573f761861e264e9e43c2cc26695b03dbdeb05f022e576",
                SwarmSoc.ReplicasOwner,
                originalChunk);

            var sourceChunkStore = new MemoryChunkStore();
            await sourceChunkStore.AddAsync(replicaSoc);
            var chunkStore = new ReplicaResolverChunkStore(sourceChunkStore, RedundancyLevel.Paranoid, new Hasher());

            // Run.
            var chunkResult = await chunkStore.GetAsync(originalHash);

            // Assert.
            Assert.Equal(originalChunk, chunkResult);
        }

        [Fact]
        public async Task OriginalReadFailureIsPreferredOverReplicaFailures()
        {
            // Setup.
            // The replicas fail first and the original request fails later: the failure of the original is reported.
            var levelDelay = TimeSpan.FromSeconds(1);
            var fakeTimeProvider = new FakeTimeProvider();
            SwarmHash originalHash = "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9";
            var originalFailure = new InvalidOperationException("original");
            var replicaFailure = new InvalidOperationException("replica");
            var originalRequest = new TaskCompletionSource<SwarmChunk>();
            
            ConcurrentBag<SwarmHash> requests = [];
            var sourceChunkStoreMock = new Mock<IChunkStore>();
            sourceChunkStoreMock.Setup(s => s.GetAsync(It.IsAny<SwarmHash>(), It.IsAny<CancellationToken>()))
                .Returns<SwarmHash, CancellationToken>((h, _) =>
                {
                    requests.Add(h);
                    return h == originalHash ? originalRequest.Task : Task.FromException<SwarmChunk>(replicaFailure);
                });
            
            var replicaChunkStore = new ReplicaResolverChunkStore(
                sourceChunkStoreMock.Object,
                RedundancyLevel.Medium,
                new Hasher(),
                levelDelay,
                fakeTimeProvider);

            // Run.
            var getTask = Assert.ThrowsAnyAsync<Exception>(() => replicaChunkStore.GetAsync(originalHash));
            
            // Release the replica requests of the single Medium level, once the original one is dispatched: they fail
            // as soon as they run. Then fail the original request, the last one the resolver sees.
            var timeout = Stopwatch.StartNew();
            while (requests.Count < 1 && timeout.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(1);
            fakeTimeProvider.Advance(levelDelay);
            while (requests.Count < 3 && timeout.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(1);
            originalRequest.SetException(originalFailure);
            var thrown = await getTask;

            // Assert.
            Assert.Same(originalFailure, thrown);
        }

        [Theory, MemberData(nameof(ReadOnlyChunkStoreBaseTest.ReadFailures), MemberType = typeof(ReadOnlyChunkStoreBaseTest))]
        public async Task ReadFailurePropagatesWhenNothingIsFound(Exception readFailure)
        {
            // Setup.
            // The original request fails and the replicas are not found: the failure is reported, not a missing chunk.
            var levelDelay = TimeSpan.FromSeconds(1);
            var fakeTimeProvider = new FakeTimeProvider();
            SwarmHash originalHash = "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9";
            
            ConcurrentBag<SwarmHash> requests = [];
            var sourceChunkStoreMock = new Mock<IChunkStore>();
            sourceChunkStoreMock.Setup(s => s.GetAsync(It.IsAny<SwarmHash>(), It.IsAny<CancellationToken>()))
                .Returns<SwarmHash, CancellationToken>(async (h, _) =>
                {
                    requests.Add(h);
                    await Task.Yield();
                    throw h == originalHash ? readFailure : new KeyNotFoundException();
                });
            
            var replicaChunkStore = new ReplicaResolverChunkStore(
                sourceChunkStoreMock.Object,
                RedundancyLevel.Medium,
                new Hasher(),
                levelDelay,
                fakeTimeProvider);

            // Run.
            var getTask = Assert.ThrowsAnyAsync<Exception>(() => replicaChunkStore.GetAsync(originalHash));
            
            // Release the replica requests of the single Medium level, once the original one is dispatched.
            var timeout = Stopwatch.StartNew();
            while (requests.Count < 1 && timeout.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(1);
            fakeTimeProvider.Advance(levelDelay);
            var thrown = await getTask;

            // Assert.
            Assert.Same(readFailure, thrown);
            Assert.Equal(3, requests.Count); //the original and both replicas were tried
        }

        [Fact]
        public async Task ReplicaReadFailurePropagatesWhenOriginalIsNotFound()
        {
            // Setup.
            // The original request is not found and the replicas fail: the failure of a replica is reported.
            var levelDelay = TimeSpan.FromSeconds(1);
            var fakeTimeProvider = new FakeTimeProvider();
            SwarmHash originalHash = "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9";
            var replicaFailure = new InvalidOperationException("replica");
            
            ConcurrentBag<SwarmHash> requests = [];
            var sourceChunkStoreMock = new Mock<IChunkStore>();
            sourceChunkStoreMock.Setup(s => s.GetAsync(It.IsAny<SwarmHash>(), It.IsAny<CancellationToken>()))
                .Returns<SwarmHash, CancellationToken>((h, _) =>
                {
                    requests.Add(h);
                    return Task.FromException<SwarmChunk>(h == originalHash ? new KeyNotFoundException() : replicaFailure);
                });
            
            var replicaChunkStore = new ReplicaResolverChunkStore(
                sourceChunkStoreMock.Object,
                RedundancyLevel.Medium,
                new Hasher(),
                levelDelay,
                fakeTimeProvider);

            // Run.
            var getTask = Assert.ThrowsAnyAsync<Exception>(() => replicaChunkStore.GetAsync(originalHash));
            
            // Release the replica requests of the single Medium level, once the original one is dispatched.
            var timeout = Stopwatch.StartNew();
            while (requests.Count < 1 && timeout.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(1);
            fakeTimeProvider.Advance(levelDelay);
            var thrown = await getTask;

            // Assert.
            Assert.Same(replicaFailure, thrown);
            Assert.Equal(3, requests.Count); //the original and both replicas were tried
        }
    }
}