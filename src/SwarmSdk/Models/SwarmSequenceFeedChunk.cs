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

using Etherna.SwarmSdk.Stores;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Models
{
    public sealed class SwarmSequenceFeedChunk : SwarmFeedChunkBase
    {
        // Consts.
        public const int LegacyTimeStampSize = sizeof(ulong);
        
        // Internal constructors.
        internal SwarmSequenceFeedChunk(SwarmSoc soc, SwarmFeedIndexBase index, SwarmFeedTopic topic) :
            base(topic, index, soc.Identifier, soc.Owner, soc.InnerChunk, soc.Signature)
        { }

        internal SwarmSequenceFeedChunk(
            SwarmFeedTopic topic,
            SwarmSequenceFeedIndex index,
            SwarmSocIdentifier identifier,
            EthAddress owner,
            SwarmCac innerChunk,
            SwarmSocSignature? signature) :
            base(topic, index, identifier, owner, innerChunk, signature)
        { }
        
        // Static builders.
        public static SwarmFeedChunkBase BuildFromSoc(SwarmSoc soc, SwarmFeedIndexBase index, SwarmFeedTopic topic)
        {
            ArgumentNullException.ThrowIfNull(soc);
            return new SwarmSequenceFeedChunk(soc, index, topic);
        }

        public static SwarmSequenceFeedChunk BuildNew(
            SwarmSequenceFeed feed,
            SwarmSequenceFeedIndex index,
            ReadOnlyMemory<byte> data,
            SwarmChunkBmt swarmChunkBmt)
        {
            ArgumentNullException.ThrowIfNull(feed);
            return BuildNew(feed.Topic, index, feed.Owner, data, swarmChunkBmt);
        }

        public static SwarmSequenceFeedChunk BuildNew(
            SwarmFeedTopic topic,
            SwarmSequenceFeedIndex index,
            EthAddress owner,
            ReadOnlyMemory<byte> data,
            SwarmChunkBmt swarmChunkBmt)
        {
            ArgumentNullException.ThrowIfNull(swarmChunkBmt);

            return new SwarmSequenceFeedChunk(
                topic,
                index,
                BuildIdentifier(topic, index, swarmChunkBmt.Hasher),
                owner,
                BuildInnerChunk(data, swarmChunkBmt),
                null);
        }

        // Properties.
        public ReadOnlyMemory<byte> Data => FeedPayload;

        /// <summary>
        /// True when the payload length matches a legacy (v1) [timestamp][reference] update,
        /// either with an unencrypted or an encrypted reference.
        /// </summary>
        public bool IsLegacyLengthPayload =>
            FeedPayload.Length is LegacyTimeStampSize + SwarmHash.HashSize or   // unencrypted ref
                                  LegacyTimeStampSize + SwarmHash.HashSize * 2; // encrypted ref

        // Methods.
        public override async Task<SwarmFeedResolvedChunk> ResolveWrappedChunkAsync(
            SwarmChunkBmt swarmChunkBmt,
            IReadOnlyChunkStore chunkStore,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(chunkStore);

            // A v1 (legacy) payload embeds a [timestamp][reference] pointing to the actual data chunk,
            // while a v2 payload embeds the data chunk directly. When the payload length matches the
            // legacy size the interpretation is ambiguous: Bee races both and keeps the one whose chunk
            // is retrievable. We reproduce the same outcome, preferring v1 only when its referenced
            // chunk can actually be fetched, and falling back to the embedded v2 chunk otherwise.
            // This matches Bee in every real case (legacy with a present manifest -> v1; ambiguous-length
            // v2 -> v2). It intentionally diverges only in the degenerate case where neither
            // interpretation is retrievable (e.g. a legacy update whose referenced chunk is gone): there
            // Bee is non-deterministic (it may 404 or serve the payload depending on which lookup wins
            // the race), while we deterministically return the embedded payload we already hold.
            if (IsLegacyLengthPayload &&
                await chunkStore.TryGetAsync(new SwarmHash(FeedPayload[LegacyTimeStampSize..]), cancellationToken)
                    .ConfigureAwait(false) is SwarmCac legacyChunk)
                return new SwarmFeedResolvedChunk(legacyChunk, SwarmFeedPayloadVersion.V1);

            return new SwarmFeedResolvedChunk(InnerChunk, SwarmFeedPayloadVersion.V2);
        }
        
        // Static methods.
        public static SwarmCac BuildInnerChunk(
            ReadOnlyMemory<byte> spanData,
            SwarmChunkBmt swarmChunkBmt)
        {
            ArgumentNullException.ThrowIfNull(swarmChunkBmt);
            
            var innerChunkHash = swarmChunkBmt.Hash(spanData);
            var innerChunk = new SwarmCac(innerChunkHash, spanData);
            return innerChunk;
        }
    }
}
