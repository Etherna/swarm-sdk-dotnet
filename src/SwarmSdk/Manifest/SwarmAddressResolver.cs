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
using Etherna.SwarmSdk.Stores;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Manifest
{
    /// <summary>
    /// Resolve Swarm addresses to their resources, traversing mantaray manifests
    /// </summary>
    public static class SwarmAddressResolver
    {
        // Static methods.
        /// <summary>
        /// Resolve the reference of an address resource through its manifest
        /// </summary>
        /// <param name="address">The resource address</param>
        /// <param name="chunkStore">The chunk store</param>
        /// <param name="redundancyLevel">Redundancy level used to retrieve the replicas of every node of the manifest</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The resolved reference</returns>
        public static async Task<SwarmReference> ResolveReferenceAsync(
            SwarmAddress address,
            IReadOnlyChunkStore chunkStore,
            RedundancyLevel redundancyLevel = RedundancyLevel.Paranoid,
            CancellationToken cancellationToken = default) =>
            (await ResolveResourceInfoAsync(
                address,
                chunkStore,
                ManifestPathResolver.IdentityResolver,
                redundancyLevel,
                cancellationToken: cancellationToken).ConfigureAwait(false)).Result.Reference;

        /// <summary>
        /// Resolve a reference from a hash or an address string
        /// </summary>
        /// <param name="referenceOrAddress">A hash, or an address (root reference + path)</param>
        /// <param name="chunkStore">The chunk store</param>
        /// <param name="redundancyLevel">Redundancy level used to retrieve the replicas of every node of the manifest</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The resolved reference</returns>
        public static async Task<SwarmReference> ResolveReferenceAsync(
            string referenceOrAddress,
            IReadOnlyChunkStore chunkStore,
            RedundancyLevel redundancyLevel = RedundancyLevel.Paranoid,
            CancellationToken cancellationToken = default)
        {
            if (SwarmHash.IsValidHash(referenceOrAddress))
                return new SwarmReference(SwarmHash.FromString(referenceOrAddress), null);
            return await ResolveReferenceAsync(
                SwarmAddress.FromString(referenceOrAddress),
                chunkStore,
                redundancyLevel,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolve an address (root reference + path) to its manifest resource info
        /// </summary>
        /// <param name="address">The resource address</param>
        /// <param name="chunkStore">The chunk store</param>
        /// <param name="manifestPathResolver">The manifest path resolver</param>
        /// <param name="redundancyLevel">Redundancy level used to retrieve the replicas of every node of the manifest</param>
        /// <param name="redundancyStrategy">Base strategy used to retrieve parity chunks</param>
        /// <param name="redundancyStrategyFallback">Fallback to more aggressive redundancy strategy if required</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The manifest path resolution result</returns>
        public static async Task<ManifestPathResolutionResult<MantarayResourceInfo>> ResolveResourceInfoAsync(
            SwarmAddress address,
            IReadOnlyChunkStore chunkStore,
            ManifestPathResolver manifestPathResolver,
            RedundancyLevel redundancyLevel = RedundancyLevel.Paranoid,
            RedundancyStrategy redundancyStrategy = RedundancyStrategy.Data,
            bool redundancyStrategyFallback = true,
            CancellationToken cancellationToken = default)
        {
            var rootManifest = await ReferencedMantarayManifest.BuildNewAsync(
                address.Reference,
                chunkStore,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback,
                cancellationToken).ConfigureAwait(false);

            return await rootManifest.GetResourceInfoAsync(
                address.Path, manifestPathResolver).ConfigureAwait(false);
        }

        /// <summary>
        /// Try to get the file name metadata of an address resource
        /// </summary>
        /// <param name="address">The resource address</param>
        /// <param name="chunkStore">The chunk store</param>
        /// <param name="redundancyLevel">Redundancy level used to retrieve the replicas of every node of the manifest</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The file name, or null if not present</returns>
        public static async Task<string?> TryGetFileNameAsync(
            SwarmAddress address,
            IReadOnlyChunkStore chunkStore,
            RedundancyLevel redundancyLevel = RedundancyLevel.Paranoid,
            CancellationToken cancellationToken = default)
        {
            var info = await ResolveResourceInfoAsync(
                address,
                chunkStore,
                ManifestPathResolver.IdentityResolver,
                redundancyLevel,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return info.Result.Metadata.GetValueOrDefault(ManifestEntry.FilenameKey);
        }
    }
}
