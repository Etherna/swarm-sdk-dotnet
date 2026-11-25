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
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Manifest
{
    public sealed class ReferencedMantarayManifest(ReferencedMantarayNode rootNode)
        : MantarayManifestBase
    {
        // Static builders.
        public static ReferencedMantarayManifest BuildNew(
            SwarmReference rootReference,
            IReadOnlyChunkStore chunkStore,
            RedundancyLevel redundancyLevel = RedundancyLevel.Paranoid,
            RedundancyStrategy redundancyStrategy = RedundancyStrategy.Data,
            bool redundancyStrategyFallback = true)
        {
            var node = new ReferencedMantarayNode(
                rootReference,
                chunkStore,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback,
                null,
                NodeType.Edge);
            return new ReferencedMantarayManifest(node);
        }

        public static ReferencedMantarayManifest BuildNew(
            SwarmCac rootChunk,
            SwarmReference rootChunkReference,
            IReadOnlyChunkStore chunkStore,
            RedundancyLevel redundancyLevel = RedundancyLevel.Paranoid,
            RedundancyStrategy redundancyStrategy = RedundancyStrategy.Data,
            bool redundancyStrategyFallback = true)
        {
            var node = new ReferencedMantarayNode(
                rootChunk,
                rootChunkReference,
                chunkStore,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback,
                null,
                NodeType.Edge);
            return new ReferencedMantarayManifest(node);
        }

        /// <summary>
        /// Build a new manifest from its root reference, fetching and decoding the root node
        /// </summary>
        /// <param name="rootReference">The manifest root reference</param>
        /// <param name="chunkStore">The chunk store</param>
        /// <param name="redundancyLevel">Redundancy level used to retrieve the replicas of every node of the manifest</param>
        /// <param name="redundancyStrategy">Base strategy used to retrieve parity chunks</param>
        /// <param name="redundancyStrategyFallback">Fallback to more aggressive redundancy strategy if required</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The manifest, with root node fetched and decoded</returns>
        public static async Task<ReferencedMantarayManifest> BuildNewAsync(
            SwarmReference rootReference,
            IReadOnlyChunkStore chunkStore,
            RedundancyLevel redundancyLevel = RedundancyLevel.Paranoid,
            RedundancyStrategy redundancyStrategy = RedundancyStrategy.Data,
            bool redundancyStrategyFallback = true,
            CancellationToken cancellationToken = default)
        {
            var node = new ReferencedMantarayNode(
                rootReference,
                chunkStore,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback,
                null,
                NodeType.Edge);
            await node.FetchChunkAsync(cancellationToken).ConfigureAwait(false);
            await node.DecodeFromChunkAsync(cancellationToken).ConfigureAwait(false);
            return new ReferencedMantarayManifest(node);
        }

        // Properties.
        public override IReadOnlyMantarayNode RootNode => rootNode;

        // Methods.
        public override Task<SwarmReference> GetReferenceAsync(
            Hasher hasher,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RootNode.Reference);
    }
}