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

using Etherna.SwarmSdk.Extensions;
using Etherna.SwarmSdk.Stores;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Models
{
    public sealed class SwarmEpochFeedChunk : SwarmFeedChunkBase
    {
        // Consts.
        public const int MaxDataSize = SwarmCac.DataSize - TimeStampSize;
        public const int TimeStampSize = sizeof(ulong);
        
        // Internal constructors.
        internal SwarmEpochFeedChunk(SwarmSoc soc, SwarmFeedIndexBase index, SwarmFeedTopic topic) :
            base(topic, index, soc.Identifier, soc.Owner, soc.InnerChunk, soc.Signature)
        { }
        
        internal SwarmEpochFeedChunk(
            SwarmFeedTopic topic,
            SwarmEpochFeedIndex index,
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
            return new SwarmEpochFeedChunk(soc, index, topic);
        }

        public static SwarmEpochFeedChunk BuildNew(
            SwarmEpochFeed feed,
            SwarmEpochFeedIndex index,
            ReadOnlyMemory<byte> data,
            SwarmChunkBmt swarmChunkBmt,
            DateTimeOffset? timestamp = null)
        {
            ArgumentNullException.ThrowIfNull(feed);
            return BuildNew(feed.Topic, index, feed.Owner, data, swarmChunkBmt, timestamp);
        }

        public static SwarmEpochFeedChunk BuildNew(
            SwarmFeedTopic topic,
            SwarmEpochFeedIndex index,
            EthAddress owner,
            ReadOnlyMemory<byte> data,
            SwarmChunkBmt swarmChunkBmt,
            DateTimeOffset? timestamp = null)
        {
            ArgumentNullException.ThrowIfNull(swarmChunkBmt);

            return new SwarmEpochFeedChunk(
                topic,
                index,
                BuildIdentifier(topic, index, swarmChunkBmt.Hasher),
                owner,
                BuildInnerChunk(data, timestamp, swarmChunkBmt),
                null);
        }

        // Properties.
        public ReadOnlyMemory<byte> SpanData => FeedPayload[TimeStampSize..];
        public DateTimeOffset TimeStamp => FeedPayload[..TimeStampSize].Span.UnixTimeSecondsToDateTimeOffset();

        // Methods.
        public override Task<SwarmFeedResolvedChunk> ResolveWrappedChunkAsync(
            SwarmChunkBmt swarmChunkBmt,
            IReadOnlyChunkStore chunkStore,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SwarmFeedResolvedChunk(UnwrapDataChunk(swarmChunkBmt), SwarmFeedPayloadVersion.V2));
        
        public SwarmCac UnwrapDataChunk(
            SwarmChunkBmt swarmChunkBmt)
        {
            ArgumentNullException.ThrowIfNull(swarmChunkBmt);
            
            var dataChunkHash = swarmChunkBmt.Hash(SpanData);
            return new SwarmCac(dataChunkHash, SpanData);
        }
        
        // Static methods.
        public static SwarmCac BuildInnerChunk(
            ReadOnlyMemory<byte> data,
            DateTimeOffset? timestamp,
            SwarmChunkBmt swarmChunkBmt)
        {
            ArgumentNullException.ThrowIfNull(swarmChunkBmt);
            if (data.Length > MaxDataSize)
                throw new ArgumentOutOfRangeException(
                    nameof(data),
                    $"Epoch feed chunk data can't be longer than {MaxDataSize} bytes");

            var payload = new byte[TimeStampSize + data.Length];
            
            var timestampByteArray = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSecondsByteArray();
            timestampByteArray.CopyTo(payload, 0);
            
            data.CopyTo(payload.AsMemory()[TimeStampSize..]);
            
            var innerChunkData = payload;
            var innerChunkSpan = SwarmCac.LengthToSpan((ulong)innerChunkData.Length);
            var innerChunkHash = swarmChunkBmt.Hash(innerChunkSpan, innerChunkData);
            var innerChunk = new SwarmCac(innerChunkHash, innerChunkSpan, innerChunkData);
            return innerChunk;
        }
    }
}
