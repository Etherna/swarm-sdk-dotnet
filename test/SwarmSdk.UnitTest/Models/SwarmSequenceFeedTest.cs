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
using Etherna.SwarmSdk.Stores;
using Microsoft.Extensions.Time.Testing;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Range = Moq.Range;

namespace Etherna.SwarmSdk.Models
{
    public class SwarmSequenceFeedTest
    {
        // Internal classes.
        public enum LookupEntryPoint
        {
            BuildNextFeedChunk,
            BuildNextFeedChunkOfTheBase,
            TryFindFeedChunkAt
        }

        public record LookupSequenceFeedTestElement(
            SwarmSequenceFeedIndex? KnownNearIndex,
            Action<Mock<IReadOnlyChunkStore>> SetupChunkStore,
            SwarmSequenceFeedChunk? ExpectedResult,
            ulong[] ExpectedIndexLookups,
            ulong[] ExpectedOptionalIndexLookups);
        
        // Consts.
        private static readonly SwarmSequenceFeed SequenceFeed = new(
            new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 },
            new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 });

        // Fields.
        private readonly Mock<IReadOnlyChunkStore> chunkStoreMock = new();

        // Data.
        public static IEnumerable<object[]> LookupSequenceFeedTests
        {
            get
            {
                var tests = new List<LookupSequenceFeedTestElement>
                {
                    // Simple lookup without known near index.
                    new(KnownNearIndex: null,
                        SetupChunkStore: chunkStoreMock =>
                        {
                            for (ulong i = 0; i <= 10; i++)
                            {
                                var chunk = BuildSequenceFeedChunk(i);
                                chunkStoreMock.Setup(c => c.TryGetAsync(
                                        chunk.Hash,
                                        It.IsAny<CancellationToken>()))
                                    .ReturnsAsync(chunk);
                            }
                        },
                        ExpectedIndexLookups: [0, 1, 3, 7, 15, 8, 10, 14, 11, 13, 31, 63, 127, 255],
                        ExpectedOptionalIndexLookups: [],
                        ExpectedResult: BuildSequenceFeedChunk(10)),
                    
                    // Simple lookup with known near index.
                    new(KnownNearIndex: new SwarmSequenceFeedIndex(5),
                        SetupChunkStore: chunkStoreMock =>
                        {
                            for (ulong i = 0; i <= 10; i++)
                            {
                                var chunk = BuildSequenceFeedChunk(i);
                                chunkStoreMock.Setup(c => c.TryGetAsync(
                                        chunk.Hash,
                                        It.IsAny<CancellationToken>()))
                                    .ReturnsAsync(chunk);
                            }
                        },
                        ExpectedIndexLookups: [5, 6, 8, 12, 9, 11, 10, 20, 36, 68, 132, 260],
                        ExpectedOptionalIndexLookups: [],
                        ExpectedResult: BuildSequenceFeedChunk(10)),
                    
                    // Simple lookup with known near index that points to last chunk.
                    new(KnownNearIndex: new SwarmSequenceFeedIndex(10),
                        SetupChunkStore: chunkStoreMock =>
                        {
                            for (ulong i = 0; i <= 10; i++)
                            {
                                var chunk = BuildSequenceFeedChunk(i);
                                chunkStoreMock.Setup(c => c.TryGetAsync(
                                        chunk.Hash,
                                        It.IsAny<CancellationToken>()))
                                    .ReturnsAsync(chunk);
                            }
                        },
                        ExpectedIndexLookups: [10, 11, 13, 17, 25, 41, 73, 137, 265],
                        ExpectedOptionalIndexLookups: [],
                        ExpectedResult: BuildSequenceFeedChunk(10)),
                    
                    // Simple lookup with known near index that points to not existing chunk.
                    new(KnownNearIndex: new SwarmSequenceFeedIndex(15),
                        SetupChunkStore: chunkStoreMock =>
                        {
                            for (ulong i = 0; i <= 10; i++)
                            {
                                var chunk = BuildSequenceFeedChunk(i);
                                chunkStoreMock.Setup(c => c.TryGetAsync(
                                        chunk.Hash,
                                        It.IsAny<CancellationToken>()))
                                    .ReturnsAsync(chunk);
                            }
                        },
                        ExpectedIndexLookups: [15],
                        ExpectedOptionalIndexLookups: [],
                        ExpectedResult: null),
                    
                    // Lookup on a feed with a long sequence of chunks (> 255).
                    new(KnownNearIndex: null,
                        SetupChunkStore: chunkStoreMock =>
                        {
                            for (ulong i = 0; i <= 300; i++)
                            {
                                var chunk = BuildSequenceFeedChunk(i);
                                chunkStoreMock.Setup(c => c.TryGetAsync(
                                        chunk.Hash,
                                        It.IsAny<CancellationToken>()))
                                    .ReturnsAsync(chunk);
                            }
                        },
                        ExpectedIndexLookups: [0, 1, 3, 7, 15, 31, 63, 127, 255, 256, 258, 262, 270, 286, 318, 287, 289, 293, 301, 294, 296, 300, 317, 382, 510],
                        ExpectedOptionalIndexLookups: [],
                        ExpectedResult: BuildSequenceFeedChunk(300)),
                    
                    // Lookup with a false negative chunk's get.
                    new(KnownNearIndex: null,
                        SetupChunkStore: chunkStoreMock =>
                        {
                            for (ulong i = 0; i <= 10; i++)
                            {
                                if (i == 3)
                                    continue;
                            
                                var chunk = BuildSequenceFeedChunk(i);
                                chunkStoreMock.Setup(c => c.TryGetAsync(
                                        chunk.Hash,
                                        It.IsAny<CancellationToken>()))
                                    .ReturnsAsync(chunk);
                            }
                        },
                        ExpectedIndexLookups: [0, 1, 3, 7, 15, 8, 10, 14, 11, 13, 31, 63, 127, 255],
                        ExpectedOptionalIndexLookups: [2],
                        ExpectedResult: BuildSequenceFeedChunk(10))
                };

                return tests.Select(t => new object[] { t });
            }
        }

        // Tests.
        
        [Theory]
        [InlineData(LookupEntryPoint.BuildNextFeedChunk)]
        [InlineData(LookupEntryPoint.BuildNextFeedChunkOfTheBase)]
        [InlineData(LookupEntryPoint.TryFindFeedChunkAt)]
        public async Task LookupCancelledAtItsFirstReadThrowsTheCancellation(LookupEntryPoint entryPoint)
        {
            // Setup.
            var chunkStore = new PendingReadChunkStore(new MemoryChunkStore(), (_, _) => true);
            using var cancellationTokenSource = new CancellationTokenSource();

            // Act.
            Task lookup = entryPoint switch
            {
                LookupEntryPoint.BuildNextFeedChunk => SequenceFeed.BuildNextFeedChunkAsync(
                    new byte[] { 1, 2, 3 },
                    (SwarmSequenceFeedIndex?)null,
                    chunkStore,
                    new SwarmChunkBmt(),
                    cancellationTokenSource.Token),
                LookupEntryPoint.BuildNextFeedChunkOfTheBase => ((SwarmFeedBase)SequenceFeed).BuildNextFeedChunkAsync(
                    new byte[] { 1, 2, 3 },
                    null,
                    chunkStore,
                    new SwarmChunkBmt(),
                    cancellationToken: cancellationTokenSource.Token),
                LookupEntryPoint.TryFindFeedChunkAt => SequenceFeed.TryFindFeedChunkAtAsync(
                    0, null, chunkStore, new Hasher(), cancellationTokenSource.Token),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint), entryPoint, null)
            };
            await chunkStore.PendingReadStarted.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellationTokenSource.CancelAsync();

            // Assert.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lookup.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(1, chunkStore.Reads);
        }

        [Theory, MemberData(nameof(LookupSequenceFeedTests))]
        public async Task LookupSequenceFeed(LookupSequenceFeedTestElement test)
        {
            // Setup.
            var hasher = new Hasher();
            test.SetupChunkStore(chunkStoreMock);

            // Act.
            var result = await SequenceFeed.TryFindLastFeedChunkAsync(
                test.KnownNearIndex,
                chunkStoreMock.Object,
                requestsCustomTimeout: null);

            // Assert.
            Assert.Equal(test.ExpectedResult?.Hash, result?.Hash);
                        
            chunkStoreMock.Verify(cs => cs.TryGetAsync(
                    It.IsAny<SwarmHash>(),
                    It.IsAny<CancellationToken>()),
                Times.Between(
                    test.ExpectedIndexLookups.Length,
                    test.ExpectedIndexLookups.Length + test.ExpectedOptionalIndexLookups.Length,
                    Range.Inclusive));
            
            foreach (var index in test.ExpectedIndexLookups)
            {
                var hash = SequenceFeed.BuildHash(new SwarmSequenceFeedIndex(index), hasher);
                chunkStoreMock.Verify(cs => cs.TryGetAsync(
                        hash,
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            }
            
            foreach (var index in test.ExpectedOptionalIndexLookups)
            {
                var hash = SequenceFeed.BuildHash(new SwarmSequenceFeedIndex(index), hasher);
                chunkStoreMock.Verify(cs => cs.TryGetAsync(
                        hash,
                        It.IsAny<CancellationToken>()),
                    Times.AtMostOnce);
            }
        }

        [Fact]
        public async Task LookupSequenceFeedTerminatesUnnecessaryPendingRequests()
        {
            // Setup.
            // A virtual clock drives the lookups' timeout: while it stays frozen the timeout can't elapse,
            // so the lower level lookups (unnecessary once the highest existing chunk has been found) can
            // only complete if the lookup actively terminates them. This proves the early termination
            // without depending on wall-clock timing.
            var fakeTimeProvider = new FakeTimeProvider();
            var sequenceFeed = new SwarmSequenceFeed(SequenceFeed.Owner, SequenceFeed.Topic, fakeTimeProvider);

            for (ulong i = 0; i <= 10; i++)
            {
                var chunk = BuildSequenceFeedChunk(i);
                chunkStoreMock.Setup(c => c.TryGetAsync(
                        chunk.Hash,
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(chunk);
            }

            // The lower level lookups of the first interval (indexes 1 and 3) stay pending until their
            // request is cancelled, simulating slow requests that the lookup must not wait for.
            ulong[] pendingIndexes = [1, 3];
            foreach (var pendingIndex in pendingIndexes)
            {
                var pendingHash = BuildSequenceFeedChunk(pendingIndex).Hash;
                chunkStoreMock.Setup(c => c.TryGetAsync(
                        pendingHash,
                        It.IsAny<CancellationToken>()))
                    .Returns<SwarmHash, CancellationToken>(async (_, ct) =>
                    {
                        try { await Task.Delay(Timeout.Infinite, ct); }
                        catch (OperationCanceledException) { }
                        return null;
                    });
            }

            // Act.
            // Start the lookup without awaiting: with the clock frozen, it can complete only by
            // terminating the pending lower level lookups.
            var lookupTask = sequenceFeed.TryFindLastFeedChunkAsync(null, chunkStoreMock.Object);

            var realTimeBound = Stopwatch.StartNew();
            while (!lookupTask.IsCompleted && realTimeBound.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(1);

            // Assert.
            Assert.True(lookupTask.IsCompleted, "Lookup did not terminate the unnecessary pending requests.");
            var result = await lookupTask;
            Assert.Equal(BuildSequenceFeedChunk(10).Hash, result?.Hash);
        }

        /// <summary>
        /// Over a feed updated up to index 10, the read of index 10 times out: it counts as not found, and the lookup
        /// answers the last update it reads, at index 9
        /// </summary>
        [Fact]
        public async Task TryFindLastFeedChunkAnswersTheLastUpdateItReadsWhenAnExistingOneTimesOut()
        {
            // Setup.
            var (sequenceFeed, chunkStore, fakeTimeProvider) = await BuildFeedWithAPendingReadAsync(10);

            // Act.
            var lookup = sequenceFeed.TryFindLastFeedChunkAsync(null, chunkStore);
            await chunkStore.PendingReadStarted.WaitAsync(TimeSpan.FromSeconds(10));
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(11));

            // Assert.
            var result = await lookup.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(BuildSequenceFeedChunk(9).Hash, result?.Hash);
        }

        [Fact]
        public async Task TryFindLastFeedChunkCancelledAfterItsFirstReadStartsNoOtherRead()
        {
            // Setup.
            // A store that cancels the caller while it answers index 0, the first read of the lookup.
            using var cancellationTokenSource = new CancellationTokenSource();
            var firstChunk = BuildSequenceFeedChunk(0);
            var reads = 0;
            chunkStoreMock.Setup(c => c.TryGetAsync(
                    It.IsAny<SwarmHash>(),
                    It.IsAny<CancellationToken>()))
                .Returns<SwarmHash, CancellationToken>(async (hash, _) =>
                {
                    Interlocked.Increment(ref reads);
                    if (hash != firstChunk.Hash)
                        return null;
                    await cancellationTokenSource.CancelAsync();
                    return firstChunk;
                });

            // Act.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => SequenceFeed.TryFindLastFeedChunkAsync(
                    null, chunkStoreMock.Object, cancellationToken: cancellationTokenSource.Token));

            // Assert.
            Assert.Equal(1, reads);
        }

        /// <summary>
        /// Over a feed updated up to index 10 the lookup reads index 0, then 1, 3, 7, 15 and on up to 255 together,
        /// then 8, 10 and 14 together, then 11 and 13 together. Each pending index here is one whose answer the
        /// lookup can't tell its result without
        /// </summary>
        [Theory]
        [InlineData(0UL)]
        [InlineData(13UL)]
        [InlineData(14UL)]
        [InlineData(255UL)]
        public async Task TryFindLastFeedChunkCancelledAtAReadThrowsTheCancellation(ulong pendingIndex)
        {
            // Setup.
            // With the clock frozen no request ends by its timeout: a pending read ends only when its token
            // is cancelled.
            var sequenceFeed = new SwarmSequenceFeed(SequenceFeed.Owner, SequenceFeed.Topic, new FakeTimeProvider());
            var innerChunkStore = new MemoryChunkStore();
            for (ulong i = 0; i <= 10; i++)
                await innerChunkStore.AddAsync(BuildSequenceFeedChunk(i));
            var pendingHash = sequenceFeed.BuildHash(new SwarmSequenceFeedIndex(pendingIndex), new Hasher());
            var chunkStore = new PendingReadChunkStore(innerChunkStore, (hash, _) => hash == pendingHash);
            using var cancellationTokenSource = new CancellationTokenSource();

            // Act.
            var lookup = sequenceFeed.TryFindLastFeedChunkAsync(
                null, chunkStore, cancellationToken: cancellationTokenSource.Token);
            await chunkStore.PendingReadStarted.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellationTokenSource.CancelAsync();

            // Assert.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lookup.WaitAsync(TimeSpan.FromSeconds(10)));
        }

        /// <summary>
        /// Over a feed updated up to index 10, the read of an index after the last one times out: it counts as not
        /// found, as an index after the last one is
        /// </summary>
        [Theory]
        [InlineData(13UL)]
        [InlineData(14UL)]
        [InlineData(255UL)]
        public async Task TryFindLastFeedChunkCountsAProbeTimedOutAsNotFound(ulong pendingIndex)
        {
            // Setup.
            var (sequenceFeed, chunkStore, fakeTimeProvider) = await BuildFeedWithAPendingReadAsync(pendingIndex);

            // Act.
            var lookup = sequenceFeed.TryFindLastFeedChunkAsync(null, chunkStore);
            await chunkStore.PendingReadStarted.WaitAsync(TimeSpan.FromSeconds(10));
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(11));

            // Assert.
            var result = await lookup.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(BuildSequenceFeedChunk(10).Hash, result?.Hash);
        }

        /// <summary>
        /// The first read tells an empty feed from a feed with updates: the timeout of the probes doesn't end it,
        /// the caller's token does
        /// </summary>
        [Fact]
        public async Task TryFindLastFeedChunkDoesNotBoundItsFirstRead()
        {
            // Setup.
            var (sequenceFeed, chunkStore, fakeTimeProvider) = await BuildFeedWithAPendingReadAsync(0);
            using var cancellationTokenSource = new CancellationTokenSource();

            // Act.
            var lookup = sequenceFeed.TryFindLastFeedChunkAsync(
                null, chunkStore, cancellationToken: cancellationTokenSource.Token);
            await chunkStore.PendingReadStarted.WaitAsync(TimeSpan.FromSeconds(10));
            fakeTimeProvider.Advance(TimeSpan.FromHours(1));
            await Task.WhenAny(lookup, Task.Delay(TimeSpan.FromMilliseconds(200)));

            // Assert.
            Assert.False(lookup.IsCompleted);
            await cancellationTokenSource.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lookup.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(1, chunkStore.Reads);
        }

        // Helpers.
        /// <summary>
        /// A sequence feed on a frozen clock, over a store with its updates from index 0 to 10, where the read of
        /// one index pends until its token is cancelled
        /// </summary>
        private static async Task<(SwarmSequenceFeed, PendingReadChunkStore, FakeTimeProvider)>
            BuildFeedWithAPendingReadAsync(ulong pendingIndex)
        {
            var fakeTimeProvider = new FakeTimeProvider();
            var sequenceFeed = new SwarmSequenceFeed(SequenceFeed.Owner, SequenceFeed.Topic, fakeTimeProvider);
            var innerChunkStore = new MemoryChunkStore();
            for (ulong i = 0; i <= 10; i++)
                await innerChunkStore.AddAsync(BuildSequenceFeedChunk(i));
            var pendingHash = sequenceFeed.BuildHash(new SwarmSequenceFeedIndex(pendingIndex), new Hasher());
            return (sequenceFeed,
                new PendingReadChunkStore(innerChunkStore, (hash, _) => hash == pendingHash),
                fakeTimeProvider);
        }

        private static SwarmSequenceFeedChunk BuildSequenceFeedChunk(ulong i)
        {
            var chunk = SwarmSequenceFeedChunk.BuildNew(
                SequenceFeed,
                new SwarmSequenceFeedIndex(i),
                BitConverter.GetBytes(i),
                new SwarmChunkBmt());
            chunk.BuildHash(new Hasher());
            return chunk;
        }
    }
}