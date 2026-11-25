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
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Manifest;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Services
{
    public interface IFeedService
    {
        Task<SwarmFeedBase?> TryDecodeFeedManifestAsync(
            ReferencedMantarayManifest manifest,
            CancellationToken cancellationToken = default);

        Task<SwarmReference> UploadFeedManifestAsync(
            SwarmFeedBase swarmFeed,
            Hasher hasher,
            ushort compactLevel = 0,
            IPostageStamper? postageStamper = null,
            IChunkStore? chunkStore = null,
            CancellationToken cancellationToken = default);
    }
}