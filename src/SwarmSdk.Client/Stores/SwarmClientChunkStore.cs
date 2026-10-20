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

using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Stores
{
    public class SwarmClientChunkStore(ISwarmClient swarmClient)
        : ReadOnlyChunkStoreBase
    {
        // Fields.
        private readonly ConcurrentQueue<SwarmChunkBmt> swarmChunkBmtPool = new();
        
        // Methods.
        public override async Task<SwarmChunk> GetAsync(SwarmHash hash, CancellationToken cancellationToken = default)
        {
            // Get chunk trying to reuse bmts with concurrency.
            if (!swarmChunkBmtPool.TryDequeue(out var swarmChunkBmt))
                swarmChunkBmt = new SwarmChunkBmt();

            try
            {
                return await swarmClient.GetChunkAsync(
                    hash,
                    swarmChunkBmt,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (SwarmSdkApiException e) when (e.StatusCode == 404)
            {
                throw new KeyNotFoundException($"Chunk {hash} not found", e);
            }
            finally
            {
                swarmChunkBmt.Clear();
                swarmChunkBmtPool.Enqueue(swarmChunkBmt);
            }
        }

        public override Task<bool> HasChunkAsync(SwarmHash hash, CancellationToken cancellationToken = default) =>
            swarmClient.IsChunkExistingAsync(hash, cancellationToken: cancellationToken);
    }
}