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

namespace Etherna.SwarmSdk.Hashing.Postage
{
    public class PostageStampIssuer : IPostageStampIssuer
    {
        // Fields.
        private readonly PostageBuckets _buckets;
        
        // Constructor.
        public PostageStampIssuer(
            PostageBatch postageBatch,
            EthAddress? postageBatchOwner = null,
            PostageBuckets? buckets = null)
        {
            ArgumentNullException.ThrowIfNull(postageBatch);
            if (!postageBatch.Depth.HasValue)
                throw new ArgumentException("Batch depth can't be null");

            _buckets = buckets ?? new PostageBuckets();
            BucketUpperBound = (uint)1 << (postageBatch.Depth.Value - PostageBatch.BucketDepth);
            PostageBatch = postageBatch;
            PostageBatchOwner = postageBatchOwner;
        }

        // Properties.
        public IReadOnlyPostageBuckets Buckets => _buckets;
        public uint BucketUpperBound { get; }
        public bool HasSaturated { get; private set; }
        public PostageBatch PostageBatch { get; }
        public EthAddress? PostageBatchOwner { get; }

        // Methods.
        public PostageBucketIndex IncrementBucketCount(SwarmHash hash)
        {
            var bucketId = hash.ToBucketId();

            //the slots of a bucket are numbered from 0, as bee does: the collisions of a bucket are its next slot
            var collisions = _buckets.GetCollisions(bucketId);
            if (collisions >= BucketUpperBound)
            {
                if (PostageBatch.IsImmutable)
                    throw new PostageBucketFullException(PostageBatch.Id, bucketId);
                HasSaturated = true;
                _buckets.ResetBucketCollisions(bucketId);
                collisions = 0;
            }

            _buckets.IncrementCollisions(bucketId);

            return new PostageBucketIndex(bucketId, collisions);
        }
    }
}