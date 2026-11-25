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
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Manifest;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Services
{
    public class FeedService : IFeedService
    {
        // Consts.
        public const string FeedMetadataEntryOwner = "swarm-feed-owner";
        public const string FeedMetadataEntryTopic = "swarm-feed-topic";
        public const string FeedMetadataEntryType  = "swarm-feed-type";
        
        // Methods.
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        public async Task<SwarmFeedBase?> TryDecodeFeedManifestAsync(
            ReferencedMantarayManifest manifest,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            
            var metadata = (await manifest.GetMetadataAsync(
                MantarayManifestBase.RootPath,
                ManifestPathResolver.IdentityResolver,
                cancellationToken).ConfigureAwait(false)).Result;
            if (!metadata.TryGetValue(FeedMetadataEntryOwner, out var hexOwner))
                return null;
            if (!metadata.TryGetValue(FeedMetadataEntryTopic, out var hexTopic))
                return null;
            if (!metadata.TryGetValue(FeedMetadataEntryType, out var strType))
                return null;

            try
            {
                var owner = hexOwner.HexToByteArray();
                var topic = hexTopic.HexToByteArray();

                return Enum.Parse<SwarmFeedType>(strType, true) switch
                {
                    SwarmFeedType.Epoch => new SwarmEpochFeed(owner, topic),
                    SwarmFeedType.Sequence => new SwarmSequenceFeed(owner, topic),
                    _ => throw new InvalidOperationException()
                };
            }
            catch
            {
                return null;
            }
        }
        
        public async Task<SwarmReference> UploadFeedManifestAsync(
            SwarmFeedBase swarmFeed,
            Hasher hasher,
            ushort compactLevel = 0,
            IPostageStamper? postageStamper = null,
            IChunkStore? chunkStore = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(swarmFeed);
            
            // Init.
            chunkStore ??= new FakeChunkStore();
            postageStamper ??= new PostageStamper(
                new FakeSigner(),
                new PostageStampIssuer(PostageBatch.MaxDepthInstance),
                new MemoryStampStore());

            // Create manifest.
            var feedManifest = new WritableMantarayManifest(
                chunkStore,
                postageStamper,
                RedundancyLevel.None,
                false,
                compactLevel,
                null);

            feedManifest.Add(
                MantarayManifestBase.RootPath,
                ManifestEntry.NewFile(
                    SwarmReference.PlainZero,
                    new Dictionary<string, string>
                    {
                        [FeedMetadataEntryOwner] = swarmFeed.Owner.ToByteArray().ToHex(),
                        [FeedMetadataEntryTopic] = swarmFeed.Topic.ToString(),
                        [FeedMetadataEntryType] = swarmFeed.Type.ToString()
                    }));

            return await feedManifest.GetReferenceAsync(hasher, cancellationToken).ConfigureAwait(false);
        }
    }
}