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

using Etherna.SwarmSdk.Models;
using System;
using System.ComponentModel;

namespace Etherna.SwarmSdk.Exceptions
{
    /// <summary>
    /// A chunk can't be stamped, because its bucket of an immutable postage batch has no slot left: the batch
    /// needs a greater depth.
    /// </summary>
    public sealed class PostageBucketFullException : InvalidOperationException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'batchId' and 'bucketId' arguments instead.", error: true)]
        public PostageBucketFullException()
        { }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'batchId' and 'bucketId' arguments instead.", error: true)]
        public PostageBucketFullException(string? message) : base(message)
        { }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'batchId' and 'bucketId' arguments instead.", error: true)]
        public PostageBucketFullException(string? message, Exception? innerException)
            : base(message, innerException)
        { }

        public PostageBucketFullException(PostageBatchId batchId, ushort bucketId)
            : base($"Bucket {bucketId} of postage batch {batchId} is full")
        {
            BatchId = batchId;
            BucketId = bucketId;
        }

        // Properties.
        public PostageBatchId BatchId { get; }
        public ushort BucketId { get; }
    }
}
