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
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Chunks
{
    public class ChunkReplicator
    {
        // Fields.
        private readonly IChunkStore chunkStore;
        private readonly IPostageStamper postageStamper;
        private readonly RedundancyLevel redundancyLevel;
        private readonly ISigner signer;

        // Constructor.
        public ChunkReplicator(
            RedundancyLevel redundancyLevel,
            IChunkStore chunkStore,
            IPostageStamper postageStamper,
            ISigner signer)
        {
            this.redundancyLevel = redundancyLevel;
            this.chunkStore = chunkStore;
            this.postageStamper = postageStamper;
            this.signer = signer ?? throw new ArgumentNullException(nameof(signer));

            if (signer.PublicAddress != SwarmSoc.ReplicasOwner)
                throw new ArgumentException("Signer has invalid owner for replicas");
        }

        // Methods.
        public async Task AddChunkReplicasAsync(
            SwarmCac chunk,
            Hasher hasher,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(chunk);
            
            if (redundancyLevel == RedundancyLevel.None)
                return;

            List<Task> tasks = [];
            var replicaHeaders = GenerateReplicaHeaders(chunk.Hash, redundancyLevel, new Hasher());
            foreach (var replicaHeader in replicaHeaders)
            {
                var replicaSoc = new SwarmSoc(replicaHeader.SocId, signer.PublicAddress, chunk);
                replicaSoc.Sign(signer, hasher);

                postageStamper.Stamp(replicaHeader.Hash);
                tasks.Add(chunkStore.AddAsync(replicaSoc, cancellationToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        
        // Static methods.
        /// <summary>
        /// Lazy generate an array of replica Ids from a hash and a redundancy level.
        /// Order of replicas is so that addresses are always maximally dispersed in successive sets of addresses.
        /// </summary>
        /// <param name="hash">Input hash</param>
        /// <param name="redundancyLevel">Input redundancy level</param>
        /// <param name="hasher">Hasher</param>
        /// <returns>Replica Ids</returns>
        public static IEnumerable<SwarmReplicaHeader> GenerateReplicaHeaders(
            SwarmHash hash,
            RedundancyLevel redundancyLevel,
            Hasher hasher)
        {
            ArgumentNullException.ThrowIfNull(hasher);

            if (redundancyLevel == RedundancyLevel.None)
                yield break;
            
            // For the five levels of redundancy, the actual numbers of replicas needed to keep
            // the error rate below 1/10^6 are [0, 2, 4, 5, 19], but we use an approximation to powers of 2.
            var targetReplicas = 1 << (int)redundancyLevel;
            var foundNeighborhoodsByDepth = new bool[(int)redundancyLevel][];
            for (var i = 1; i <= (int)redundancyLevel; i++)
                foundNeighborhoodsByDepth[i - 1] = new bool[1 << i];
            var foundReplicas = 0;
            
            // Build queue and cursor.
            var queue = new SwarmReplicaHeader?[targetReplicas];
            var queueCursorsByDepth = new int[(int)redundancyLevel];
            for (int i = 1; i < (int)redundancyLevel; i++)
                queueCursorsByDepth[i] = 1 << i;
            
            // Search replica ids.
            for (byte i = 0; i < 255 && foundReplicas < targetReplicas; i++)
            {
                // Generate Soc Id and Hash.
                var socIdArray = hash.ToByteArray();
                socIdArray[0] = i;
                var socHash = SwarmSoc.BuildHash(socIdArray, SwarmSoc.ReplicasOwner, hasher);

                // Try to add new replica to queue, and drain it.
                var isValid = TryAddAtDepth(
                    new SwarmReplicaHeader
                    {
                        Hash = socHash,
                        SocId = socIdArray
                    },
                    (int)redundancyLevel,
                    foundNeighborhoodsByDepth,
                    queueCursorsByDepth,
                    queue);

                if (!isValid) continue;
                
                for (var j = foundReplicas; j < queue.Length; j++)
                {
                    if (queue[j] == null) break;
                    foundReplicas++;
                    yield return queue[j]!;
                }
            }
        }

        // Helpers.
        private static bool TryAddAtDepth(
            SwarmReplicaHeader replicaHeader,
            int depth,
            bool[][] foundNeighborhoodsByDepth,
            int[] queueCursorsByDepth,
            SwarmReplicaHeader?[] queue)
        {
            if (depth == 0)
                return false;

            // Find neighborhood and verify collisions.
            var neighborhood = replicaHeader.Hash.ToReadOnlyMemory().Span[0] >> (8 - (byte)depth);
            if (foundNeighborhoodsByDepth[depth - 1][neighborhood])
                return false;
            foundNeighborhoodsByDepth[depth - 1][neighborhood] = true;
            
            // Try to add at lower depth.
            if (TryAddAtDepth(replicaHeader, depth - 1, foundNeighborhoodsByDepth, queueCursorsByDepth, queue))
                return true;
            
            // If recursion didn't insert at lower depth, insert here.
            queue[queueCursorsByDepth[depth - 1]] = replicaHeader;
            queueCursorsByDepth[depth - 1]++;

            return true;
        }
    }
}