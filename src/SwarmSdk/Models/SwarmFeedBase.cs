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
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Models
{
    public abstract class SwarmFeedBase(EthAddress owner, SwarmFeedTopic topic)
    {
        // Consts.
        protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
        
        // Properties.
        public EthAddress Owner { get; } = owner;
        public SwarmFeedTopic Topic { get; } = topic;
        public abstract SwarmFeedType Type { get; }
        
        // Methods.
        public SwarmHash BuildHash(SwarmFeedIndexBase index, Hasher hasher) =>
            SwarmFeedChunkBase.BuildHash(Topic, index, Owner, hasher);

        public SwarmSocIdentifier BuildIdentifier(SwarmFeedIndexBase index, Hasher hasher) =>
            SwarmFeedChunkBase.BuildIdentifier(Topic, index, hasher);
        
        public abstract Task<SwarmFeedChunkBase> BuildNextFeedChunkAsync(
            ReadOnlyMemory<byte> data,
            SwarmFeedIndexBase? knownNearIndex,
            IReadOnlyChunkStore chunkStore,
            SwarmChunkBmt chunkBmt,
            DateTimeOffset? timestamp = null,
            CancellationToken cancellationToken = default);
        
        public abstract SwarmFeedChunkBase SocToFeedChunk(SwarmSoc soc, SwarmFeedIndexBase index);

        /// <summary>
        /// Try to find feed at a given time
        /// </summary>
        /// <param name="at">The time to search</param>
        /// <param name="knownNearIndex">Another known existing index, near to looked time. Helps to perform lookup quicker</param>
        /// <param name="chunkStore">The chunk store</param>
        /// <param name="hasher">The hasher</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The found feed chunk, or null</returns>
        public abstract Task<SwarmFeedChunkBase?> TryFindFeedChunkAtAsync(
            long at,
            SwarmFeedIndexBase? knownNearIndex,
            IReadOnlyChunkStore chunkStore,
            Hasher hasher,
            CancellationToken cancellationToken = default);

        public async Task<SwarmFeedChunkBase?> TryGetFeedChunkAsync(
            SwarmFeedIndexBase index,
            IReadOnlyChunkStore chunkStore,
            Hasher hasher,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(chunkStore);
            ArgumentNullException.ThrowIfNull(hasher);

            var hash = BuildHash(index, hasher);

            var chunk = await chunkStore.TryGetAsync(
                hash,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (chunk is not SwarmSoc soc)
                return null;

            return SocToFeedChunk(soc, index);
        }
    }
}