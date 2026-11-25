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
    public abstract class SwarmFeedChunkBase(
        SwarmFeedTopic topic,
        SwarmFeedIndexBase index,
        SwarmSocIdentifier identifier,
        EthAddress owner,
        SwarmCac innerChunk,
        SwarmSocSignature? signature)
        : SwarmSoc(identifier, owner, innerChunk, null, signature)
    {
        // Properties.
        public ReadOnlyMemory<byte> FeedPayload => InnerChunk.Data;
        public SwarmFeedIndexBase Index { get; } = index ?? throw new ArgumentNullException(nameof(index));
        public SwarmFeedTopic Topic { get; } = topic;

        // Methods.
        /// <summary>
        /// Resolve the wrapped data chunk, automatically detecting whether the feed update uses a
        /// legacy (v1) or current (v2) payload. Mirrors Bee's resolution: when the payload length is
        /// ambiguous the legacy interpretation is preferred only if its referenced chunk is actually
        /// retrievable, otherwise it falls back to the embedded (v2) chunk.
        /// </summary>
        /// <param name="swarmChunkBmt">The BMT used to rebuild the embedded chunk when needed.</param>
        /// <param name="chunkStore">The store used to resolve a legacy reference.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        public abstract Task<SwarmFeedResolvedChunk> ResolveWrappedChunkAsync(
            SwarmChunkBmt swarmChunkBmt,
            IReadOnlyChunkStore chunkStore,
            CancellationToken cancellationToken = default);
        
        // Static methods.
        public static SwarmHash BuildHash(
            SwarmFeedTopic topic,
            SwarmFeedIndexBase index,
            EthAddress owner,
            Hasher hasher) =>
            BuildHash(BuildIdentifier(topic, index, hasher), owner, hasher);
        
        public static SwarmSocIdentifier BuildIdentifier(
            SwarmFeedTopic topic,
            SwarmFeedIndexBase index,
            Hasher hasher)
        {
            ArgumentNullException.ThrowIfNull(hasher);
            ArgumentNullException.ThrowIfNull(index);

            return hasher.ComputeHash([topic.ToReadOnlyMemory(), index.MarshalBinary()]);
        }
    }
}
