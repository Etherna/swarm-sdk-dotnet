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

using System;
using Xunit;

namespace Etherna.SwarmSdk.Models
{
    public class PostageBucketsTest
    {
        // Tests.
        [Fact]
        public void ConstructorAllocatesTheBucketsOnly()
        {
            // Run.
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            using var buckets = new PostageBuckets();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            // Assert.
            //the counters of 65,536 buckets take 256 KiB
            Assert.True(allocated < 300 * 1024, $"{allocated} bytes allocated building the buckets");
        }

        [Fact]
        public void ConstructorReadsTheCollisionsOfInitialBuckets()
        {
            // Setup.
            var initialBuckets = new uint[PostageBuckets.BucketsSize];
            Array.Fill(initialBuckets, 2u);
            initialBuckets[7] = 5;
            initialBuckets[9] = 1;

            // Run.
            using var buckets = new PostageBuckets(initialBuckets);

            // Assert.
            Assert.Equal(5u, buckets.MaxBucketCollisions);
            Assert.Equal(1u, buckets.MinBucketCollisions);
            Assert.Equal(PostageBuckets.CollisionsToRequiredPostageBatchDepth(5), buckets.RequiredPostageBatchDepth);
            Assert.Equal([0, 1, PostageBuckets.BucketsSize - 2, 0, 0, 1], buckets.CountBucketsByCollisions());
            Assert.Equal([9u], buckets.GetBucketsByCollisions(1));
        }

        [Fact]
        public void IncrementCollisionsMovesABucketToTheNextLevel()
        {
            // Setup.
            using var buckets = new PostageBuckets();

            // Run.
            buckets.IncrementCollisions(3);
            buckets.IncrementCollisions(3);
            buckets.IncrementCollisions(8);

            // Assert.
            Assert.Equal(2u, buckets.GetCollisions(3));
            Assert.Equal(2u, buckets.MaxBucketCollisions);
            Assert.Equal(0u, buckets.MinBucketCollisions);
            Assert.Equal(3, buckets.TotalChunks);
            Assert.Equal([PostageBuckets.BucketsSize - 2, 1, 1], buckets.CountBucketsByCollisions());
            Assert.Equal([8u], buckets.GetBucketsByCollisions(1));
            Assert.Equal([3u], buckets.GetBucketsByCollisions(2));
        }

        [Fact]
        public void IncrementCollisionsRaisesTheMinimumWhenItsLevelEmpties()
        {
            // Setup.
            var initialBuckets = new uint[PostageBuckets.BucketsSize];
            Array.Fill(initialBuckets, 1u);
            initialBuckets[4] = 0;
            using var buckets = new PostageBuckets(initialBuckets);

            // Run.
            buckets.IncrementCollisions(4);

            // Assert.
            Assert.Equal(1u, buckets.MinBucketCollisions);
            Assert.Equal(1u, buckets.MaxBucketCollisions);
            Assert.Empty(buckets.GetBucketsByCollisions(0));
        }

        [Fact]
        public void ResetBucketCollisionsLowersTheMaximumToTheFullestBucketLeft()
        {
            // Setup.
            using var buckets = new PostageBuckets();
            buckets.IncrementCollisions(3);
            buckets.IncrementCollisions(3);
            buckets.IncrementCollisions(3);
            buckets.IncrementCollisions(8);

            // Run.
            buckets.ResetBucketCollisions(3);

            // Assert.
            Assert.Equal(0u, buckets.GetCollisions(3));
            Assert.Equal(1u, buckets.MaxBucketCollisions);
            Assert.Equal(0u, buckets.MinBucketCollisions);
            Assert.Equal([PostageBuckets.BucketsSize - 1, 1, 0, 0], buckets.CountBucketsByCollisions());
        }
    }
}
