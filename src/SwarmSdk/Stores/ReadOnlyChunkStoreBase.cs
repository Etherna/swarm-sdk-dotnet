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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Stores
{
    public abstract class ReadOnlyChunkStoreBase : IReadOnlyChunkStore
    {
        // Methods.
        public abstract Task<SwarmChunk> GetAsync(
            SwarmHash hash,
            CancellationToken cancellationToken = default);

        public virtual async Task<IReadOnlyDictionary<SwarmHash, SwarmChunk?>> GetAsync(
            IEnumerable<SwarmHash> hashes,
            int? canReturnAfterFailed = null,
            int? canReturnAfterSucceeded = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(hashes);
            
            //cancel all pendent load tasks when returning before all of them are completed
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var tasks = hashes.Select(async hash =>
            {
                try { return await GetAsync(hash, cts.Token).ConfigureAwait(false); }
                catch (KeyNotFoundException) { return null; }
            }).ToList();

            Dictionary<SwarmHash, SwarmChunk?> results = [];
            var failedChunks = 0;
            while (tasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(completedTask);
                
                var chunkResult = await completedTask.ConfigureAwait(false);
                if (chunkResult != null)
                    results.Add(chunkResult.Hash, chunkResult);
                else
                    failedChunks++;

                if (canReturnAfterSucceeded <= results.Count ||
                    canReturnAfterFailed <= failedChunks)
                    break;
            }
            return results;
        }

        public virtual async Task<bool> HasChunkAsync(
            SwarmHash hash,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await GetAsync(hash, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        public async Task<SwarmChunk?> TryGetAsync(
            SwarmHash hash,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await GetAsync(hash, cancellationToken).ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
    }
}