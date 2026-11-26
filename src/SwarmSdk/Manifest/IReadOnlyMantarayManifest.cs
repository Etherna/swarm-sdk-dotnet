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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Manifest
{
    /// <summary>
    /// A manifest read by path: a referenced one, fetching its nodes from a store, or a writable one, before or after
    /// its reference is computed
    /// </summary>
    public interface IReadOnlyMantarayManifest
    {
        // Properties.
        public IReadOnlyMantarayNode RootNode { get; }

        // Methods.
        /// <summary>
        /// Get the metadata at a path of the manifest
        /// </summary>
        /// <param name="path">The path</param>
        /// <param name="pathResolver">How the path is resolved: as it is, or as a browser does with the index and
        /// error documents</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The metadata, and whether they come from the error document</returns>
        /// <exception cref="KeyNotFoundException">The manifest holds no such path</exception>
        Task<ManifestPathResolutionResult<IReadOnlyDictionary<string, string>>> GetMetadataAsync(
            string path,
            ManifestPathResolver pathResolver,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the resource at a path of the manifest: the reference of its entry and its metadata
        /// </summary>
        /// <param name="path">The path</param>
        /// <param name="pathResolver">How the path is resolved: as it is, or as a browser does with the index and
        /// error documents</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The resource, and whether it is the error document</returns>
        /// <exception cref="KeyNotFoundException">The manifest holds no such path</exception>
        Task<ManifestPathResolutionResult<MantarayResourceInfo>> GetResourceInfoAsync(
            string path,
            ManifestPathResolver pathResolver,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tell whether a path of the manifest starts with a prefix
        /// </summary>
        /// <param name="path">The prefix</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>True if a path of the manifest starts with it</returns>
        Task<bool> HasPathPrefixAsync(
            string path,
            CancellationToken cancellationToken = default);
    }
}