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
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Manifest
{
    public abstract class MantarayManifestBase : IReadOnlyMantarayManifest
    {
        // Consts.
        public static readonly string RootPath = SwarmAddress.Separator.ToString();

        // Properties.
        public abstract IReadOnlyMantarayNode RootNode { get; }
        
        // Methods.
        public abstract Task<SwarmReference> GetReferenceAsync(
            Hasher hasher,
            CancellationToken cancellationToken = default);

        public async Task<ManifestPathResolutionResult<IReadOnlyDictionary<string, string>>> GetMetadataAsync(
            string path,
            ManifestPathResolver pathResolver,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(pathResolver);
            
            await RootNode.OnVisitingAsync(cancellationToken).ConfigureAwait(false);
            return await pathResolver.InvokeAsync(
                path,
                invokeAsync: RootNode.GetMetadataAsync,
                hasPathPrefixAsync: RootNode.HasPathPrefixAsync,
                getRootMetadataAsync: ct => RootNode.GetMetadataAsync(RootPath, ct),
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<ManifestPathResolutionResult<MantarayResourceInfo>> GetResourceInfoAsync(
            string path,
            ManifestPathResolver pathResolver,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(pathResolver);
            
            await RootNode.OnVisitingAsync(cancellationToken).ConfigureAwait(false);
            return await pathResolver.InvokeAsync(
                path,
                invokeAsync: RootNode.GetResourceInfoAsync,
                hasPathPrefixAsync: RootNode.HasPathPrefixAsync,
                getRootMetadataAsync: ct => RootNode.GetMetadataAsync(RootPath, ct),
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> HasPathPrefixAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(path);
            
            await RootNode.OnVisitingAsync(cancellationToken).ConfigureAwait(false);
            return await RootNode.HasPathPrefixAsync(
                path.TrimStart(SwarmAddress.Separator),
                cancellationToken).ConfigureAwait(false);
        }
    }
}