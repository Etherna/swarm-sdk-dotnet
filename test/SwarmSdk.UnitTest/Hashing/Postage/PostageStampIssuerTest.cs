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
using System;
using Xunit;

namespace Etherna.SwarmSdk.Hashing.Postage
{
    public class PostageStampIssuerTest
    {
        // Consts.
        private const ushort BucketId = 0x1234;

        // Tests.
        [Fact]
        public void IncrementBucketCountGoesOnFromTheCollisionsOfInitialBuckets()
        {
            // Setup.
            //the buckets of a batch count its chunks, as bee reports them: the next slot is the count
            var initialBuckets = new uint[PostageBuckets.BucketsSize];
            initialBuckets[BucketId] = 3;
            using var buckets = new PostageBuckets(initialBuckets);
            var issuer = new PostageStampIssuer(BuildPostageBatch(20, false), buckets: buckets);

            // Run.
            var index = issuer.IncrementBucketCount(BuildHash(0x01));

            // Assert.
            Assert.Equal(3u, index.BucketCounter);
            Assert.Equal(4u, buckets.GetCollisions(BucketId));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IncrementBucketCountIssuesTheSlotsOfABucketFromZero(bool isImmutable)
        {
            // Setup.
            //a batch of depth 17 has two slots per bucket
            var issuer = new PostageStampIssuer(BuildPostageBatch(17, isImmutable));

            // Run.
            var first = issuer.IncrementBucketCount(BuildHash(0x01));
            var last = issuer.IncrementBucketCount(BuildHash(0x02));

            // Assert.
            Assert.Equal(2u, issuer.BucketUpperBound);
            Assert.Equal(BucketId, first.BucketId);
            Assert.Equal(0u, first.BucketCounter);
            Assert.Equal(issuer.BucketUpperBound - 1, last.BucketCounter);
            Assert.False(issuer.HasSaturated);
        }

        [Fact]
        public void IncrementBucketCountOnAFullBucketOfAMutableBatchRestartsFromSlotZero()
        {
            // Setup.
            using var buckets = new PostageBuckets();
            var issuer = new PostageStampIssuer(BuildPostageBatch(17, false), buckets: buckets);
            issuer.IncrementBucketCount(BuildHash(0x01));
            issuer.IncrementBucketCount(BuildHash(0x02));

            // Run.
            var index = issuer.IncrementBucketCount(BuildHash(0x03));

            // Assert.
            Assert.Equal(0u, index.BucketCounter);
            Assert.Equal(1u, buckets.GetCollisions(BucketId));
            Assert.True(issuer.HasSaturated);
        }

        [Fact]
        public void IncrementBucketCountOnAFullBucketOfAnImmutableBatchThrows()
        {
            // Setup.
            using var buckets = new PostageBuckets();
            var batch = BuildPostageBatch(17, true);
            var issuer = new PostageStampIssuer(batch, buckets: buckets);
            issuer.IncrementBucketCount(BuildHash(0x01));
            issuer.IncrementBucketCount(BuildHash(0x02));

            // Run.
            var exception = Assert.Throws<PostageBucketFullException>(
                () => issuer.IncrementBucketCount(BuildHash(0x03)));

            // Assert.
            Assert.Equal(batch.Id, exception.BatchId);
            Assert.Equal(BucketId, exception.BucketId);
            Assert.Equal(2u, buckets.GetCollisions(BucketId));
            Assert.False(issuer.HasSaturated);
        }

        // Helpers.
        private static SwarmHash BuildHash(byte last)
        {
            var hash = new byte[SwarmHash.HashSize];
            hash[0] = BucketId >> 8;
            hash[1] = BucketId & 0xff;
            hash[^1] = last;
            return new SwarmHash(hash);
        }

        private static PostageBatch BuildPostageBatch(int depth, bool isImmutable) => new(
            id: PostageBatchId.Zero,
            amount: 0,
            blockNumber: 0,
            depth: depth,
            exists: true,
            isImmutable: isImmutable,
            isUsable: true,
            label: null,
            ttl: TimeSpan.Zero,
            utilization: 0);
    }
}
