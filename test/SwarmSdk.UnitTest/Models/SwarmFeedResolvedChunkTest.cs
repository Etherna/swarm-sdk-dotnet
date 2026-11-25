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
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Models
{
    public class SwarmFeedResolvedChunkTest
    {
        // Consts.
        private static readonly byte[] Owner =
            [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19];
        private static readonly byte[] Topic =
            [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31];

        // Fields.
        private readonly Mock<IReadOnlyChunkStore> chunkStoreMock = new();

        // Tests.

        [Fact]
        public async Task ResolveSequenceV2EmbeddedPayload()
        {
            // Setup.
            // A non legacy-sized payload embeds the data chunk directly: no resolution is needed.
            var spanData = BuildSpanData(new byte[100]);
            var feedChunk = SwarmSequenceFeedChunk.BuildNew(
                new SwarmSequenceFeed(Owner, Topic),
                new SwarmSequenceFeedIndex(0),
                spanData,
                new SwarmChunkBmt());

            // Action.
            var result = await feedChunk.ResolveWrappedChunkAsync(new SwarmChunkBmt(), chunkStoreMock.Object);

            // Assert.
            Assert.Equal(SwarmFeedPayloadVersion.V2, result.Version);
            Assert.Equal(feedChunk.InnerChunk.Hash, result.Chunk.Hash);
            chunkStoreMock.Verify(
                c => c.TryGetAsync(It.IsAny<SwarmHash>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ResolveSequenceV1LegacyPayload()
        {
            // Setup.
            // A legacy-sized payload [timestamp][reference] is resolved to the referenced chunk
            // when that chunk is retrievable.
            var referenceBytes = new byte[SwarmHash.HashSize];
            for (byte i = 0; i < referenceBytes.Length; i++)
                referenceBytes[i] = i;
            var referenceHash = new SwarmHash(referenceBytes);
            var referencedChunk = SwarmCac.BuildFromData(referenceHash, new byte[] { 10, 20, 30 });

            var payload = new byte[SwarmSequenceFeedChunk.LegacyTimeStampSize + SwarmHash.HashSize];
            referenceBytes.CopyTo(payload, SwarmSequenceFeedChunk.LegacyTimeStampSize);
            var feedChunk = SwarmSequenceFeedChunk.BuildNew(
                new SwarmSequenceFeed(Owner, Topic),
                new SwarmSequenceFeedIndex(0),
                BuildSpanData(payload),
                new SwarmChunkBmt());

            chunkStoreMock.Setup(c => c.TryGetAsync(referenceHash, It.IsAny<CancellationToken>()))
                .ReturnsAsync(referencedChunk);

            // Action.
            var result = await feedChunk.ResolveWrappedChunkAsync(new SwarmChunkBmt(), chunkStoreMock.Object);

            // Assert.
            Assert.Equal(SwarmFeedPayloadVersion.V1, result.Version);
            Assert.Equal(referenceHash, result.Chunk.Hash);
        }

        [Fact]
        public async Task ResolveSequenceAmbiguousLengthFallsBackToV2()
        {
            // Setup.
            // A legacy-sized payload whose referenced chunk is not retrievable is not a legacy update:
            // it falls back to the embedded (v2) chunk.
            var payload = new byte[SwarmSequenceFeedChunk.LegacyTimeStampSize + SwarmHash.HashSize];
            var feedChunk = SwarmSequenceFeedChunk.BuildNew(
                new SwarmSequenceFeed(Owner, Topic),
                new SwarmSequenceFeedIndex(0),
                BuildSpanData(payload),
                new SwarmChunkBmt());

            chunkStoreMock.Setup(c => c.TryGetAsync(It.IsAny<SwarmHash>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwarmChunk?)null);

            // Action.
            var result = await feedChunk.ResolveWrappedChunkAsync(new SwarmChunkBmt(), chunkStoreMock.Object);

            // Assert.
            Assert.Equal(SwarmFeedPayloadVersion.V2, result.Version);
            Assert.Equal(feedChunk.InnerChunk.Hash, result.Chunk.Hash);
        }

        [Fact]
        public async Task ResolveSequenceCancelledAtTheLegacyReadThrowsTheCancellation()
        {
            // Setup.
            // A legacy-sized payload makes the resolution read the chunk it would reference.
            var payload = new byte[SwarmSequenceFeedChunk.LegacyTimeStampSize + SwarmHash.HashSize];
            var feedChunk = SwarmSequenceFeedChunk.BuildNew(
                new SwarmSequenceFeed(Owner, Topic),
                new SwarmSequenceFeedIndex(0),
                BuildSpanData(payload),
                new SwarmChunkBmt());
            var chunkStore = new PendingReadChunkStore(new MemoryChunkStore(), (_, _) => true);
            using var cancellationTokenSource = new CancellationTokenSource();

            // Action.
            var resolution = feedChunk.ResolveWrappedChunkAsync(
                new SwarmChunkBmt(), chunkStore, cancellationTokenSource.Token);
            await chunkStore.PendingReadStarted.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellationTokenSource.CancelAsync();

            // Assert.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => resolution.WaitAsync(TimeSpan.FromSeconds(10)));
        }

        [Fact]
        public async Task ResolveEpochAlwaysV2()
        {
            // Setup.
            // Epoch feeds have no legacy-reference concept: they always resolve to the embedded chunk.
            var feedChunk = SwarmEpochFeedChunk.BuildNew(
                new SwarmEpochFeed(Owner, Topic),
                new SwarmEpochFeedIndex(0, 0, new Hasher()),
                BuildSpanData([1, 2, 3]),
                new SwarmChunkBmt());

            // Action.
            var result = await feedChunk.ResolveWrappedChunkAsync(new SwarmChunkBmt(), chunkStoreMock.Object);

            // Assert.
            Assert.Equal(SwarmFeedPayloadVersion.V2, result.Version);
            Assert.Equal(feedChunk.UnwrapDataChunk(new SwarmChunkBmt()).Hash, result.Chunk.Hash);
            chunkStoreMock.Verify(
                c => c.TryGetAsync(It.IsAny<SwarmHash>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // Helpers.
        private static byte[] BuildSpanData(byte[] data)
        {
            var spanData = new byte[SwarmCac.SpanSize + data.Length];
            SwarmCac.LengthToSpan((ulong)data.Length).CopyTo(spanData, 0);
            data.CopyTo(spanData, SwarmCac.SpanSize);
            return spanData;
        }
    }
}
