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

using Etherna.SwarmSdk.Chunks;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Stores
{
    public class ReplicaResolverChunkStore(
        IReadOnlyChunkStore sourceChunkStore,
        RedundancyLevel level,
        Hasher hasher,
        TimeSpan? customLevelDelay = null,
        TimeProvider? customTimeProvider = null)
        : ReadOnlyChunkStoreBase
    {
        // Fields.
        /// <summary>
        /// Duration between successive additional requests
        /// </summary>
        private readonly TimeSpan levelDelay = customLevelDelay ?? TimeSpan.FromMilliseconds(300);
        private readonly TimeProvider timeProvider = customTimeProvider ?? TimeProvider.System;

        // Methods.
        public override async Task<SwarmChunk> GetAsync(SwarmHash hash, CancellationToken cancellationToken = default)
        {
            // Without redundancy, simply bypass.
            if (level == RedundancyLevel.None)
                return await sourceChunkStore.GetAsync(hash, cancellationToken).ConfigureAwait(false);
            
            // Otherwise, try to resolve with replicas.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                var replicaHeaders = ChunkReplicator.GenerateReplicaHeaders(hash, level, hasher);
                
                // Concurrently try to retrieve original chunk with its replicas, using cumulative delays by level.
                // Requests (for level == 4):
                //   delay * 0: original hash
                //   delay * 1: +2 replicas
                //   delay * 2: +2 replicas (tot 4)
                //   delay * 3: +4 replicas (tot 8)
                //   delay * 4: +8 replicas (tot 16)
                var originalTask = sourceChunkStore.GetAsync(hash, cts.Token);
                List<Task<SwarmChunk>> getChunkTasks = [originalTask];
                foreach (var (replicaHeader, i) in replicaHeaders.Select((r, i) => (r, i)))
                {
                    var delayMultiplier = 0;
                    var j = i; //i is immutable
                    do
                    {
                        delayMultiplier++;
                        j >>= 1;
                    } while (j > 0);
                    getChunkTasks.Add(FetchReplicaAsync(replicaHeader.Hash, levelDelay * delayMultiplier, cts.Token));
                }
                
                // Returns the first replying with success. The requests failing for any reason other than not found
                // are remembered: if nothing is found, the failure of the original request propagates, or the first
                // failure of a replica when the original was not found, instead of a misleading not found.
                Exception? originalFailure = null;
                List<Exception> replicaFailures = [];
                while (getChunkTasks.Count != 0)
                {
                    // Parallel fetch with WhenAny, first wins.
                    var completedTask = await Task.WhenAny(getChunkTasks).ConfigureAwait(false);
                    getChunkTasks.Remove(completedTask);

                    try
                    {
                        // If success, return and cancel other requests with finally block.
                        return await completedTask.ConfigureAwait(false);
                    }
                    catch (KeyNotFoundException) { }
                    catch (Exception e) when (e is InvalidOperationException or SwarmSdkApiException ||
                                              (e is OperationCanceledException && !cancellationToken.IsCancellationRequested)) //request timeout
                    {
                        if (completedTask == originalTask)
                            originalFailure = e;
                        else
                            replicaFailures.Add(e);
                    }
                }

                // Can't find chunk.
                var readFailure = originalFailure ?? replicaFailures.FirstOrDefault();
                if (readFailure is not null)
                    ExceptionDispatchInfo.Throw(readFailure);
                throw new KeyNotFoundException("All search levels exhausted. Can't find chunk or replicas");
            }
            finally
            {
                await cts.CancelAsync().ConfigureAwait(false);
            }
        }

        // Helpers.
        private async Task<SwarmChunk> FetchReplicaAsync(
            SwarmHash replicaHash,
            TimeSpan delay,
            CancellationToken cancellationToken)
        {
            // Wait the delay for this level.
            await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            
            // Get replica chunk.
            var chunk = await sourceChunkStore.GetAsync(replicaHash, cancellationToken).ConfigureAwait(false);
            if (chunk is not SwarmSoc soc)
                throw new SwarmChunkTypeException(chunk, "Chunk is not a SOC");
            return soc.InnerChunk;
        }
    }
}
