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

using Etherna.SwarmSdk.Extensions;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Stores;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Models
{
    public class SwarmEpochFeedTest
    {
        // Internal classes.
        public record FindLastEpochChunkBeforeDateTestElement(
            SwarmEpochFeed Feed,
            ulong At,
            SwarmEpochFeedChunk StartingChunk,
            Action<Mock<IReadOnlyChunkStore>> ArrangeAction,
            Action<Mock<IReadOnlyChunkStore>> Asserts,
            SwarmEpochFeedChunk ExpectedResult);

        public enum LookupEntryPoint
        {
            BuildNextFeedChunk,
            BuildNextFeedChunkOfTheBase,
            TryFindFeedChunkAt
        }

        public record FindStartingEpochOfflineTestElement(
            ulong At,
            SwarmEpochFeedIndex? KnownNearEpoch,
            SwarmEpochFeedIndex ExpectedResult);

        public record TryFindStartingEpochChunkOnlineTestElement(
            SwarmEpochFeed Feed,
            ulong At,
            SwarmEpochFeedIndex EpochIndex,
            Action<Mock<IReadOnlyChunkStore>> ArrangeAction,
            Action<Mock<IReadOnlyChunkStore>> Asserts,
            SwarmEpochFeedChunk? ExpectedResult);

        // Consts.
        private static readonly SwarmEpochFeed EpochFeed = new(
            new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 },
            new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 });
        
        // Fields.
        private readonly Mock<IReadOnlyChunkStore> chunkStoreMock = new();

        // Data.
        public static IEnumerable<object[]> FindLastEpochChunkBeforeDateTests
        {
            get
            {
                var tests = new List<FindLastEpochChunkBeforeDateTestElement>();

                // Starting chunk epoch index is at max resolution (level 0).
                {
                    var startingChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        new SwarmEpochFeedIndex(4, 0, new Hasher()),
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 5, TimeSpan.Zero));
                
                    tests.Add(new FindLastEpochChunkBeforeDateTestElement(
                        EpochFeed,
                        6,
                        startingChunk,
                        _ => { },
                        gateMock => gateMock.Verify(g => g.TryGetAsync(
                            It.IsAny<SwarmHash>(),
                            It.IsAny<CancellationToken>()), Times.Never()),
                        startingChunk));
                }

                // Chunk with child epoch at date is valid and at max resolution.
                {
                    var startingChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        new SwarmEpochFeedIndex(4, 1, new Hasher()),
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 4, TimeSpan.Zero));
                
                    var childChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        new SwarmEpochFeedIndex(5, 0, new Hasher()),
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 5, TimeSpan.Zero));
                    var childChunkHash = childChunk.BuildHash(new Hasher());
                
                    tests.Add(new FindLastEpochChunkBeforeDateTestElement(
                        EpochFeed,
                        6,
                        startingChunk,
                        gateMock => gateMock.Setup(g => g.TryGetAsync(
                                childChunkHash,
                                It.IsAny<CancellationToken>()))
                            .ReturnsAsync(childChunk),
                        gateMock => gateMock.Verify(g => g.TryGetAsync(
                            It.IsAny<SwarmHash>(),
                            It.IsAny<CancellationToken>()), Times.Once()),
                        childChunk));
                }
                
                // Chunk with left brother of child epoch at date is valid and at max resolution.
                {
                    var startingChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        new SwarmEpochFeedIndex(4, 1, new Hasher()),
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 4, TimeSpan.Zero));
                
                    var rightChildChunkHash = EpochFeed.BuildHash(new SwarmEpochFeedIndex(5, 0, new Hasher()), new Hasher());
                
                    var leftChildChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        new SwarmEpochFeedIndex(4, 0, new Hasher()),
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 4, TimeSpan.Zero));
                    var leftChildChunkHash = leftChildChunk.BuildHash(new Hasher());
                
                    tests.Add(new FindLastEpochChunkBeforeDateTestElement(
                        EpochFeed,
                        5,
                        startingChunk,
                        gateMock =>
                        {
                            gateMock.Setup(g => g.TryGetAsync(
                                    rightChildChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync((SwarmChunk?)null);
                            gateMock.Setup(g => g.TryGetAsync(
                                    leftChildChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync(leftChildChunk);
                        },
                        gateMock => gateMock.Verify(g => g.TryGetAsync(
                            It.IsAny<SwarmHash>(),
                            It.IsAny<CancellationToken>()), Times.Exactly(2)),
                        leftChildChunk));
                }
                
                // Chunk on child at date epoch is left and doesn't exist.
                {
                    var startingChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        new SwarmEpochFeedIndex(4, 2, new Hasher()),
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 4, TimeSpan.Zero));
                
                    var leftChildChunkHash = EpochFeed.BuildHash(new SwarmEpochFeedIndex(4, 1, new Hasher()), new Hasher());
                
                    tests.Add(new FindLastEpochChunkBeforeDateTestElement(
                        EpochFeed,
                        5,
                        startingChunk,
                        gateMock =>
                        {
                            gateMock.Setup(g => g.TryGetAsync(
                                    leftChildChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync((SwarmChunk?)null);
                        },
                        gateMock => gateMock.Verify(g => g.TryGetAsync(
                            It.IsAny<SwarmHash>(),
                            It.IsAny<CancellationToken>()), Times.Once()),
                        startingChunk));
                }
                
                // Chunks on child at date and its left brother epochs don't exist.
                {
                    var startingChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        new SwarmEpochFeedIndex(4, 2, new Hasher()),
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 4, TimeSpan.Zero));
                
                    var rightChildChunkHash = EpochFeed.BuildHash(new SwarmEpochFeedIndex(6, 1, new Hasher()), new Hasher());
                
                    var leftChildChunkHash = EpochFeed.BuildHash(new SwarmEpochFeedIndex(4, 1, new Hasher()), new Hasher());
                
                    tests.Add(new FindLastEpochChunkBeforeDateTestElement(
                        EpochFeed,
                        6,
                        startingChunk,
                        gateMock =>
                        {
                            gateMock.Setup(g => g.TryGetAsync(
                                    rightChildChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync((SwarmChunk?)null);
                            gateMock.Setup(g => g.TryGetAsync(
                                    leftChildChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync((SwarmChunk?)null);
                        },
                        gateMock => gateMock.Verify(g => g.TryGetAsync(
                            It.IsAny<SwarmHash>(),
                            It.IsAny<CancellationToken>()), Times.Exactly(2)),
                        startingChunk));
                }
                
                // Chunk on child at date (right) is successive to date, and chunk on its left brother epoch doesn't exist.
                {
                    var startingChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        new SwarmEpochFeedIndex(4, 2, new Hasher()),
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 4, TimeSpan.Zero));
                
                    var leftChildChunkHash = EpochFeed.BuildHash(new SwarmEpochFeedIndex(4, 1, new Hasher()), new Hasher());
                
                    var rightChildChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        new SwarmEpochFeedIndex(6, 1, new Hasher()),
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 7, TimeSpan.Zero));
                    var rightChildChunkHash = rightChildChunk.BuildHash(new Hasher());
                
                    tests.Add(new FindLastEpochChunkBeforeDateTestElement(
                        EpochFeed,
                        6,
                        startingChunk,
                        gateMock =>
                        {
                            gateMock.Setup(g => g.TryGetAsync(
                                    rightChildChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync(rightChildChunk);
                            gateMock.Setup(g => g.TryGetAsync(
                                    leftChildChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync((SwarmChunk?)null);
                        },
                        gateMock => gateMock.Verify(g => g.TryGetAsync(
                            It.IsAny<SwarmHash>(),
                            It.IsAny<CancellationToken>()), Times.Exactly(2)),
                        startingChunk));
                }

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> FindStartingEpochOfflineTests
        {
            get
            {
                var tests = new List<FindStartingEpochOfflineTestElement>
                {
                    // Null known near epoch, date on left epoch at max level.
                    new(10,
                        null,
                        new SwarmEpochFeedIndex(0, 32, new Hasher())),
                    
                    // Null known near epoch, date on right epoch at max level.
                    new(5_000_000_000,
                        null,
                        new SwarmEpochFeedIndex(4_294_967_296, 32, new Hasher())),

                    // Known near epoch prior to date.
                    new(7,
                        new SwarmEpochFeedIndex(5, 0, new Hasher()),
                        new SwarmEpochFeedIndex(4, 2, new Hasher())),

                    // Known near epoch successive to date.
                    new(8,
                        new SwarmEpochFeedIndex(14, 1, new Hasher()),
                        new SwarmEpochFeedIndex(8, 3, new Hasher())),

                    // Known near epoch contains date.
                    new(10,
                        new SwarmEpochFeedIndex(8, 2, new Hasher()),
                        new SwarmEpochFeedIndex(8, 2, new Hasher()))
                };

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> TryFindStartingEpochChunkOnlineTests
        {
            get
            {
                var tests = new List<TryFindStartingEpochChunkOnlineTestElement>();
        
                // Chunk exists and is prior to date.
                {
                    var startingEpochIndex = new SwarmEpochFeedIndex(4, 2, new Hasher());

                    var expectedResult = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        startingEpochIndex,
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 5, TimeSpan.Zero));
                    var hash = expectedResult.BuildHash(new Hasher());
                
                    tests.Add(new TryFindStartingEpochChunkOnlineTestElement(
                        EpochFeed,
                        6,
                        startingEpochIndex,
                        gateMock => gateMock.Setup(g => g.TryGetAsync(
                                hash,
                                It.IsAny<CancellationToken>()))
                            .ReturnsAsync(expectedResult),
                        gateMock => gateMock.Verify(g => g.TryGetAsync(
                            It.IsAny<SwarmHash>(),
                            It.IsAny<CancellationToken>()), Times.Once()),
                        expectedResult));
                };
        
                // Chunk not exists and index was left at max level.
                {
                    tests.Add(new TryFindStartingEpochChunkOnlineTestElement(
                        EpochFeed,
                        6,
                        new SwarmEpochFeedIndex(0, SwarmEpochFeedIndex.MaxLevel, new Hasher()),
                        gateMock => gateMock.Setup(g => g.TryGetAsync(
                                It.IsAny<SwarmHash>(),
                                It.IsAny<CancellationToken>()))
                            .ReturnsAsync((SwarmChunk?)null),
                        gateMock => gateMock.Verify(g => g.TryGetAsync(
                            It.IsAny<SwarmHash>(),
                            It.IsAny<CancellationToken>()), Times.Once()),
                        null));
                }
                
                // Chunk exists but with successive date and index was left at max level.
                {
                    var startingEpochIndex = new SwarmEpochFeedIndex(0, SwarmEpochFeedIndex.MaxLevel, new Hasher());
                    
                    var epochChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        startingEpochIndex,
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 7, TimeSpan.Zero));
                    var hash = epochChunk.BuildHash(new Hasher());
                
                    tests.Add(new TryFindStartingEpochChunkOnlineTestElement(
                        EpochFeed,
                        6,
                        startingEpochIndex,
                        gateMock => gateMock.Setup(g => g.TryGetAsync(
                                hash,
                                It.IsAny<CancellationToken>()))
                            .ReturnsAsync(epochChunk),
                        gateMock => gateMock.Verify(g => g.TryGetAsync(
                            It.IsAny<SwarmHash>(),
                            It.IsAny<CancellationToken>()), Times.Once()),
                        null));
                }
                
                // Valid chunk is found at left, starting chunk is not found.
                {
                    var startingEpochIndex = new SwarmEpochFeedIndex(6, 1, new Hasher());
                    var startingChunkHash = EpochFeed.BuildHash(startingEpochIndex, new Hasher());
                
                    var leftChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        startingEpochIndex.Left,
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 5, TimeSpan.Zero));
                    var leftChunkHash = leftChunk.BuildHash(new Hasher());
                
                    tests.Add(new TryFindStartingEpochChunkOnlineTestElement(
                        EpochFeed,
                        6,
                        startingEpochIndex,
                        gateMock =>
                        {
                            gateMock.Setup(g => g.TryGetAsync(
                                    startingChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync((SwarmChunk?)null);
                            gateMock.Setup(g => g.TryGetAsync(
                                    leftChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync(leftChunk);
                        },
                        gateMock => gateMock.Verify(g => g.TryGetAsync(
                            It.IsAny<SwarmHash>(),
                            It.IsAny<CancellationToken>()), Times.Exactly(2)),
                        leftChunk));
                }
                
                // Valid chunk is found at left, starting chunk is successive to date.
                {
                    var startingEpochIndex = new SwarmEpochFeedIndex(6, 1, new Hasher());
                    var startingChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        startingEpochIndex,
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 7, TimeSpan.Zero));
                    var startingChunkHash = startingChunk.BuildHash(new Hasher());
                
                    var leftChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        startingEpochIndex.Left,
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 5, TimeSpan.Zero));
                    var leftChunkHash = leftChunk.BuildHash(new Hasher());
                
                    tests.Add(new TryFindStartingEpochChunkOnlineTestElement(
                        EpochFeed,
                        6,
                        startingEpochIndex,
                        gateMock =>
                        {
                            gateMock.Setup(g => g.TryGetAsync(
                                    startingChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync(startingChunk);
                            gateMock.Setup(g => g.TryGetAsync(
                                    leftChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync(leftChunk);
                        },
                        gateMock => gateMock.Verify(g => g.TryGetAsync(
                            It.IsAny<SwarmHash>(),
                            It.IsAny<CancellationToken>()), Times.Exactly(2)),
                        leftChunk));
                }
                
                // Chunk valid is found at parent, starting chunk is not found.
                {
                    var startingEpochIndex = new SwarmEpochFeedIndex(4, 1, new Hasher());
                    var startingChunkHash = EpochFeed.BuildHash(startingEpochIndex, new Hasher());
                
                    var parentChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        startingEpochIndex.Parent,
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 5, TimeSpan.Zero));
                    var parentChunkHash = parentChunk.BuildHash(new Hasher());
                
                    tests.Add(new TryFindStartingEpochChunkOnlineTestElement(
                        EpochFeed,
                        6,
                        startingEpochIndex,
                        gateMock =>
                        {
                            gateMock.Setup(g => g.TryGetAsync(
                                    startingChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync((SwarmChunk?)null);
                            gateMock.Setup(g => g.TryGetAsync(
                                    parentChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync(parentChunk);
                        },
                        gateMock => gateMock.Verify(g => g.TryGetAsync(
                            It.IsAny<SwarmHash>(),
                            It.IsAny<CancellationToken>()), Times.Exactly(2)),
                        parentChunk));
                }
                
                // Chunk valid is found at parent, starting chunk is successive to date.
                {
                    var startingEpochIndex = new SwarmEpochFeedIndex(4, 1, new Hasher());
                    var startingChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        startingEpochIndex,
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 7, TimeSpan.Zero));
                    var startingChunkHash = startingChunk.BuildHash(new Hasher());
                
                    var parentChunk = SwarmEpochFeedChunk.BuildNew(
                        EpochFeed,
                        startingEpochIndex.Parent,
                        new byte[] { 1, 2, 3 },
                        new SwarmChunkBmt(),
                        new DateTimeOffset(1970, 1, 1, 0, 0, 5, TimeSpan.Zero));
                    var parentChunkHash = parentChunk.BuildHash(new Hasher());
                
                    tests.Add(new TryFindStartingEpochChunkOnlineTestElement(
                        EpochFeed,
                        6,
                        startingEpochIndex,
                        gateMock =>
                        {
                            gateMock.Setup(g => g.TryGetAsync(
                                    startingChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync(startingChunk);
                            gateMock.Setup(g => g.TryGetAsync(
                                    parentChunkHash,
                                    It.IsAny<CancellationToken>()))
                                .ReturnsAsync(parentChunk);
                        },
                        gateMock => gateMock.Verify(g => g.TryGetAsync(
                            It.IsAny<SwarmHash>(),
                            It.IsAny<CancellationToken>()), Times.Exactly(2)),
                        parentChunk));
                }
        
                return tests.Select(t => new object[] { t });
            }
        }
        
        // Tests.
        [Fact]
        public void BuildInnerChunkVerifyMaxDataSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SwarmEpochFeedChunk.BuildInnerChunk(
                    new byte[SwarmEpochFeedChunk.MaxDataSize + 1],
                    null,
                    new SwarmChunkBmt()));
        }
        
        [Fact]
        public void BuildInnerChunkSetDefaultTimestamp()
        {
            var data = new byte[] { 4, 2, 0 };

            var beforeTimeStamp = DateTimeOffset.UtcNow;
            Thread.Sleep(1000);
            var innerChunk = SwarmEpochFeedChunk.BuildInnerChunk(data, null, new SwarmChunkBmt());
            Thread.Sleep(1000);
            var afterTimeStamp = DateTimeOffset.UtcNow;
            var chunkTimeStamp = innerChunk.Data[..SwarmEpochFeedChunk.TimeStampSize].Span.UnixTimeSecondsToDateTimeOffset();

            Assert.InRange(chunkTimeStamp, beforeTimeStamp, afterTimeStamp);
            Assert.Equal(data, innerChunk.Data[SwarmEpochFeedChunk.TimeStampSize..]);
        }
        
        [Theory, MemberData(nameof(FindLastEpochChunkBeforeDateTests))]
        public async Task FindLastEpochChunkBeforeDate(FindLastEpochChunkBeforeDateTestElement test)
        {
            test.ArrangeAction(chunkStoreMock);

            var result = await test.Feed.FindLastEpochChunkBeforeDateAsync(
                chunkStoreMock.Object,
                test.At,
                test.StartingChunk,
                new Hasher(),
                CancellationToken.None);

            Assert.Equal(test.ExpectedResult, result);
            test.Asserts(chunkStoreMock);
        }

        [Theory, MemberData(nameof(FindStartingEpochOfflineTests))]
        public void FindStartingEpochOffline(FindStartingEpochOfflineTestElement test)
        {
            var result = SwarmEpochFeed.FindStartingEpochOffline(test.KnownNearEpoch, test.At, new Hasher());

            Assert.Equal(test.ExpectedResult, result);
        }
        
        [Fact]
        public void GetContentData()
        {
            var chunk = new SwarmEpochFeedChunk(
                new Hasher().ComputeHash("topic"),
                new SwarmEpochFeedIndex(0, 0, new Hasher()),
                new Hasher().ComputeHash("identifier"),
                EthAddress.Zero,
                SwarmCac.BuildFromData(SwarmHash.Zero, new byte[] { 0, 0, 0, 1, 2, 3, 4, 5, 6, 7 }),
                null);

            var result = chunk.SpanData;

            Assert.Equal(new byte[] { 6, 7 }, result);
        }

        [Fact]
        public void GetTimeStamp()
        {
            var chunk = new SwarmEpochFeedChunk(
                new Hasher().ComputeHash("topic"),
                new SwarmEpochFeedIndex(0, 0, new Hasher()),
                new Hasher().ComputeHash("identifier"),
                EthAddress.Zero,
                SwarmCac.BuildFromData(SwarmHash.Zero, new byte[] { 0, 0, 0, 1, 2, 3, 4, 5, 6, 7 }),
                null);

            var result = chunk.TimeStamp;

            Assert.Equal(new DateTimeOffset(2107, 03, 04, 22, 02, 45, TimeSpan.Zero), result);
        }

        [Theory]
        [InlineData(LookupEntryPoint.BuildNextFeedChunk)]
        [InlineData(LookupEntryPoint.BuildNextFeedChunkOfTheBase)]
        [InlineData(LookupEntryPoint.TryFindFeedChunkAt)]
        public async Task LookupCancelledAtItsFirstReadThrowsTheCancellation(LookupEntryPoint entryPoint)
        {
            // Arrange.
            var chunkStore = new PendingReadChunkStore(new MemoryChunkStore(), (_, _) => true);
            using var cancellationTokenSource = new CancellationTokenSource();

            // Act.
            Task lookup = entryPoint switch
            {
                LookupEntryPoint.BuildNextFeedChunk => EpochFeed.BuildNextFeedChunkAsync(
                    new byte[] { 1, 2, 3 },
                    (SwarmEpochFeedIndex?)null,
                    chunkStore,
                    new SwarmChunkBmt(),
                    cancellationToken: cancellationTokenSource.Token),
                LookupEntryPoint.BuildNextFeedChunkOfTheBase => ((SwarmFeedBase)EpochFeed).BuildNextFeedChunkAsync(
                    new byte[] { 1, 2, 3 },
                    null,
                    chunkStore,
                    new SwarmChunkBmt(),
                    cancellationToken: cancellationTokenSource.Token),
                LookupEntryPoint.TryFindFeedChunkAt => EpochFeed.TryFindFeedChunkAtAsync(
                    7, null, chunkStore, new Hasher(), cancellationTokenSource.Token),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint), entryPoint, null)
            };
            await chunkStore.PendingReadStarted.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellationTokenSource.CancelAsync();

            // Assert.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lookup.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(1, chunkStore.Reads);
        }

        /// <summary>
        /// The lookup of second 7, from the known epoch (6, 1), reads ten epochs through every step it can take.
        /// Upwards, to find where to start: (6, 1) and its left (4, 1), with no update, their parent (4, 2), with
        /// none, its left (0, 2), whose update is later than the second, and their parent (0, 3), where it starts.
        /// Downwards, to get nearer: (4, 2) again, its left (0, 2), which it enters, then (2, 1), which it enters,
        /// then (3, 0) and (2, 0), with no update
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        public async Task TryFindFeedAtCancelledAtAReadThrowsTheCancellationAtThatRead(int pendingRead)
        {
            // Arrange.
            var innerChunkStore = new MemoryChunkStore();
            foreach (var (start, level, timeStamp) in new (ulong, byte, long)[] { (0, 2, 9), (0, 3, 1), (2, 1, 2) })
            {
                var chunk = SwarmEpochFeedChunk.BuildNew(
                    EpochFeed,
                    new SwarmEpochFeedIndex(start, level, new Hasher()),
                    new byte[] { 1, 2, 3 },
                    new SwarmChunkBmt(),
                    DateTimeOffset.FromUnixTimeSeconds(timeStamp));
                chunk.BuildHash(new Hasher());
                await innerChunkStore.AddAsync(chunk);
            }
            var chunkStore = new PendingReadChunkStore(innerChunkStore, (_, read) => read == pendingRead);
            using var cancellationTokenSource = new CancellationTokenSource();

            // Act.
            var lookup = EpochFeed.TryFindFeedAtAsync(
                DateTimeOffset.FromUnixTimeSeconds(7),
                new SwarmEpochFeedIndex(6, 1, new Hasher()),
                chunkStore,
                new Hasher(),
                cancellationTokenSource.Token);
            await chunkStore.PendingReadStarted.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellationTokenSource.CancelAsync();

            // Assert.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lookup.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(pendingRead, chunkStore.Reads);
        }

        [Theory, MemberData(nameof(TryFindStartingEpochChunkOnlineTests))]
        public async Task TryFindStartingEpochChunkOnline(TryFindStartingEpochChunkOnlineTestElement test)
        {
            test.ArrangeAction(chunkStoreMock);
        
            var result = await test.Feed.TryFindStartingEpochChunkOnlineAsync(
                chunkStoreMock.Object,
                test.At,
                test.EpochIndex,
                new Hasher(),
                CancellationToken.None);
        
            Assert.Equal(test.ExpectedResult, result);
            test.Asserts(chunkStoreMock);
        }
        
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TryGetFeedChunkAsync(bool chunkExists)
        {
            // Arrange.
            var index = new SwarmEpochFeedIndex(2, 1, new Hasher());
            var epochChunk = SwarmEpochFeedChunk.BuildNew(
                EpochFeed,
                index,
                new byte[] { 0, 0, 0, 1, 2, 3, 4, 5, 6, 7 },
                new SwarmChunkBmt());
            var hash = epochChunk.BuildHash(new Hasher());
        
            if (chunkExists)
                chunkStoreMock.Setup(c => c.TryGetAsync(
                        hash,
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(epochChunk);
            else
                chunkStoreMock.Setup(c => c.TryGetAsync(
                        hash,
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync((SwarmChunk?)null);
        
            // Act.
            var result = await EpochFeed.TryGetFeedChunkAsync(
                index,
                chunkStoreMock.Object,
                new Hasher());
        
            // Assert.
            if (chunkExists)
                Assert.Equal(epochChunk, result);
            else
                Assert.Null(result);
        }
        
        [Fact]
        public void VerifyMaxDataSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SwarmEpochFeedChunk.BuildNew(
                    new SwarmEpochFeed(EthAddress.Zero, new Hasher().ComputeHash("")),
                    new SwarmEpochFeedIndex(0, 0, new Hasher()),
                    new byte[SwarmEpochFeedChunk.MaxDataSize + 1],
                    new SwarmChunkBmt()));
        }
    }
}