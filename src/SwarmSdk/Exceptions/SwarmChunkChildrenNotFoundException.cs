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
using System.Collections.Generic;
using System.ComponentModel;

namespace Etherna.SwarmSdk.Exceptions
{
    /// <summary>
    /// A read of a chunk tree could not fetch the children of one of its intermediate chunks: the
    /// <see cref="SwarmChunksNotFoundException"/> of their decoder, with the reference of the chunk they belong to. A
    /// <see cref="KeyNotFoundException"/>, so a caller handling a not found chunk handles it too
    /// </summary>
    public sealed class SwarmChunkChildrenNotFoundException : KeyNotFoundException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'parentReference' argument instead.", error: true)]
        public SwarmChunkChildrenNotFoundException()
        {
            MissingShards = [];
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'parentReference' argument instead.", error: true)]
        public SwarmChunkChildrenNotFoundException(string? message) : base(message)
        {
            MissingShards = [];
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'parentReference' argument instead.", error: true)]
        public SwarmChunkChildrenNotFoundException(string? message, Exception? innerException)
            : base(message, innerException)
        {
            MissingShards = [];
        }

        public SwarmChunkChildrenNotFoundException(
            SwarmReference parentReference,
            SwarmChunksNotFoundException innerException)
            : base(BuildMessage(parentReference, innerException), innerException)
        {
            MissingShards = innerException.MissingShards;
            ParentReference = parentReference;
        }

        public SwarmChunkChildrenNotFoundException(
            SwarmReference parentReference,
            IReadOnlyList<SwarmShardReference> missingShards,
            string message)
            : base(message)
        {
            MissingShards = missingShards ?? throw new ArgumentNullException(nameof(missingShards));
            ParentReference = parentReference;
        }

        public SwarmChunkChildrenNotFoundException(
            SwarmReference parentReference,
            IReadOnlyList<SwarmShardReference> missingShards,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            MissingShards = missingShards ?? throw new ArgumentNullException(nameof(missingShards));
            ParentReference = parentReference;
        }

        // Properties.
        /// <summary>
        /// The children the decoder asked for and did not obtain, data and parities
        /// </summary>
        public IReadOnlyList<SwarmShardReference> MissingShards { get; }

        /// <summary>
        /// The reference of the intermediate chunk the children belong to
        /// </summary>
        public SwarmReference ParentReference { get; }

        // Helpers.
        private static string BuildMessage(SwarmReference parentReference, SwarmChunksNotFoundException innerException)
        {
            ArgumentNullException.ThrowIfNull(innerException);

            var children = $"Children of chunk {parentReference.Hash} could not be fetched";
            return innerException.MissingShards.Count == 0 ?
                children :
                $"{children}: {SwarmChunksNotFoundException.DescribeShards(innerException.MissingShards)}";
        }
    }
}
