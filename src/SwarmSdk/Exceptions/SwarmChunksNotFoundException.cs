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
using System.Linq;

namespace Etherna.SwarmSdk.Exceptions
{
    /// <summary>
    /// A <see cref="Chunks.ChunkParityDecoder"/> asked its store for chunks and did not obtain the ones it needs. A
    /// <see cref="KeyNotFoundException"/>, so a caller handling a not found chunk handles it too
    /// </summary>
    public sealed class SwarmChunksNotFoundException : KeyNotFoundException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'missingShards' argument instead.", error: true)]
        public SwarmChunksNotFoundException()
        {
            MissingShards = [];
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'missingShards' argument instead.", error: true)]
        public SwarmChunksNotFoundException(string? message) : base(message)
        {
            MissingShards = [];
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'missingShards' argument instead.", error: true)]
        public SwarmChunksNotFoundException(string? message, Exception? innerException)
            : base(message, innerException)
        {
            MissingShards = [];
        }

        public SwarmChunksNotFoundException(IReadOnlyList<SwarmShardReference> missingShards)
            : this(missingShards, BuildMessage(missingShards))
        { }

        public SwarmChunksNotFoundException(IReadOnlyList<SwarmShardReference> missingShards, string message)
            : base(message)
        {
            MissingShards = missingShards ?? throw new ArgumentNullException(nameof(missingShards));
        }

        public SwarmChunksNotFoundException(
            IReadOnlyList<SwarmShardReference> missingShards,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            MissingShards = missingShards ?? throw new ArgumentNullException(nameof(missingShards));
        }

        // Properties.
        /// <summary>
        /// The shards the decoder asked for and did not obtain, data and parities. A fetch stops at the first failures
        /// its strategy can't absorb, so a shard whose lookup was still pending is listed with the not found ones
        /// </summary>
        public IReadOnlyList<SwarmShardReference> MissingShards { get; }

        // Internals.
        /// <summary>
        /// The hashes of the shards, comma separated, the parities marked as such
        /// </summary>
        internal static string DescribeShards(IEnumerable<SwarmShardReference> shards) =>
            string.Join(", ", shards.Select(s =>
                s.IsParity ? $"{s.Reference.Hash} (parity)" : s.Reference.Hash.ToString()));

        // Helpers.
        private static string BuildMessage(IReadOnlyList<SwarmShardReference> missingShards)
        {
            ArgumentNullException.ThrowIfNull(missingShards);

            return missingShards.Count == 0 ?
                "No chunk could be fetched" :
                $"Chunks {DescribeShards(missingShards)} could not be fetched";
        }
    }
}
