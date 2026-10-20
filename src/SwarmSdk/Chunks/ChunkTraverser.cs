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
using Etherna.SwarmSdk.Manifest;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Chunks
{
    public class ChunkTraverser(
        IReadOnlyChunkStore chunkStore)
    {
        // Public delegates.
        public delegate Task OnChunkFoundAsync(SwarmChunk chunk, SwarmShardReference shardReference);
        public delegate Task OnInvalidChunkFoundAsync(SwarmChunk chunk, SwarmShardReference shardReference);
        public delegate Task OnChunkNotFoundAsync(SwarmShardReference shardReference);
        
        // Methods.
        /// <summary>
        /// Traverse the content of a reference, identifying its root chunk: a single owner chunk is the whole
        /// traversal, a mantaray manifest root is traversed as a manifest, any other chunk as a data chunk
        /// </summary>
        /// <param name="rootReference">Root traversing chunk</param>
        public async Task TraverseAsync(
            SwarmReference rootReference,
            OnChunkFoundAsync? onChunkFound,
            OnInvalidChunkFoundAsync? onInvalidChunkFound,
            OnChunkNotFoundAsync? onChunkNotFound,
            RedundancyLevel redundancyLevel = RedundancyLevel.Insane,
            RedundancyStrategy redundancyStrategy = RedundancyStrategy.Data, 
            bool redundancyStrategyFallback = true,
            bool includeParities = false,
            CancellationToken cancellationToken = default)
        {
            onChunkFound ??= (_, _) => Task.CompletedTask;
            onInvalidChunkFound ??= (_, _) => Task.CompletedTask;
            onChunkNotFound ??= _ => Task.CompletedTask;
            
            // Upgrade redundancy strategy from None to Data.
            if (redundancyStrategy == RedundancyStrategy.None)
                redundancyStrategy = RedundancyStrategy.Data;
            
            // Resolve root chunk.
            var rootChunk = await TryGetChunkAsync(
                rootReference.Hash,
                redundancyLevel,
                cancellationToken).ConfigureAwait(false);
            var rootShardReference = new SwarmShardReference(rootReference, false);
            if (rootChunk == null)
            {
                await onChunkNotFound(rootShardReference).ConfigureAwait(false);
                return;
            }
            await onChunkFound(rootChunk, rootShardReference).ConfigureAwait(false);
            
            // A single owner chunk is the whole traversal, as bee does: don't enter the wrapped chunk.
            if (rootChunk is not SwarmCac rootCac)
                return;
            
            // Identify if is manifest root chunk.
            var rootNode = new ReferencedMantarayNode(
                rootCac,
                rootReference,
                chunkStore,
                redundancyStrategy,
                redundancyStrategyFallback,
                null,
                NodeType.Edge);
            try
            {
                rootNode.DecodeFromChunk();
            }
            catch (InvalidOperationException) //in case it's not a manifest
            {
                // The data loop marks each dequeued reference as visited, the root included: start from an empty set.
                await TraverseDataFromChunkHelperAsync(
                    rootCac,
                    rootReference,
                    [],
                    onChunkFound,
                    onChunkNotFound,
                    redundancyStrategy,
                    redundancyStrategyFallback,
                    includeParities,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            // Traverse as manifest. The decoded node helper doesn't mark its own node as visited, the resolving
            // helper does it before reading a node: the root is marked here, so a fork pointing back to it is skipped.
            await TraverseMantarayNodeFromDecodedHelperAsync(
                rootNode,
                [rootReference],
                onChunkFound,
                onInvalidChunkFound,
                onChunkNotFound,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback,
                includeParities,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task TraverseFromDataChunkAsync(
            SwarmReference reference,
            OnChunkFoundAsync? onChunkFound,
            OnChunkNotFoundAsync? onChunkNotFound,
            RedundancyLevel redundancyLevel,
            RedundancyStrategy redundancyStrategy, 
            bool redundancyStrategyFallback,
            bool includeParities,
            CancellationToken cancellationToken = default)
        {
            onChunkFound ??= (_, _) => Task.CompletedTask;
            onChunkNotFound ??= _ => Task.CompletedTask;

            // Upgrade redundancy strategy from None to Data.
            if (redundancyStrategy == RedundancyStrategy.None)
                redundancyStrategy = RedundancyStrategy.Data;

            // Read as data or intermediate chunk.
            await TraverseDataHelperAsync(
                reference,
                [],
                onChunkFound,
                onChunkNotFound,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback,
                includeParities,
                cancellationToken).ConfigureAwait(false);
        }
        
        public async Task TraverseFromMantarayManifestRootAsync(
            SwarmReference rootReference,
            OnChunkFoundAsync? onChunkFound,
            OnInvalidChunkFoundAsync? onInvalidChunkFound,
            OnChunkNotFoundAsync? onChunkNotFound,
            RedundancyLevel redundancyLevel,
            RedundancyStrategy redundancyStrategy, 
            bool redundancyStrategyFallback,
            bool includeParities,
            CancellationToken cancellationToken = default)
        {
            onChunkFound ??= (_, _) => Task.CompletedTask;
            onInvalidChunkFound ??= (_, _) => Task.CompletedTask;
            onChunkNotFound ??= _ => Task.CompletedTask;
            
            // Upgrade redundancy strategy from None to Data.
            if (redundancyStrategy == RedundancyStrategy.None)
                redundancyStrategy = RedundancyStrategy.Data;

            // Read as manifest.
            await TraverseMantarayNodeHelperAsync(
                rootReference,
                null,
                NodeType.Edge,
                [],
                onChunkFound,
                onInvalidChunkFound,
                onChunkNotFound,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback,
                includeParities,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task TraverseFromMantarayNodeChunkAsync(
            SwarmReference nodeReference,
            NodeType nodeTypeFlags,
            OnChunkFoundAsync? onChunkFound,
            OnInvalidChunkFoundAsync? onInvalidChunkFound,
            OnChunkNotFoundAsync? onChunkNotFound,
            RedundancyLevel redundancyLevel,
            RedundancyStrategy redundancyStrategy, 
            bool redundancyStrategyFallback,
            bool includeParities,
            CancellationToken cancellationToken = default)
        {
            onChunkFound ??= (_, _) => Task.CompletedTask;
            onInvalidChunkFound ??= (_, _) => Task.CompletedTask;
            onChunkNotFound ??= _ => Task.CompletedTask;

            // Upgrade redundancy strategy from None to Data.
            if (redundancyStrategy == RedundancyStrategy.None)
                redundancyStrategy = RedundancyStrategy.Data;

            // Read as manifest node.
            await TraverseMantarayNodeHelperAsync(
                nodeReference,
                null,
                nodeTypeFlags,
                [],
                onChunkFound,
                onInvalidChunkFound,
                onChunkNotFound,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback,
                includeParities,
                cancellationToken).ConfigureAwait(false);
        }

        // Helpers.
        private async Task TraverseDataFromChunkHelperAsync(
            SwarmCac rootCac,
            SwarmReference rootReference,
            HashSet<SwarmReference> visitedReferences,
            OnChunkFoundAsync onChunkFound,
            OnChunkNotFoundAsync onChunkNotFound,
            RedundancyStrategy redundancyStrategy, 
            bool redundancyStrategyFallback,
            bool includeParities,
            CancellationToken cancellationToken)
        {
            Queue<(SwarmCac Chunk, SwarmReference Reference)> chunkRefPairs = [];
            var hasher = new Hasher();
            
            // Run levels iteration starting from root chunk.
            chunkRefPairs.Enqueue((rootCac, rootReference));
            while (chunkRefPairs.Count > 0)
            {
                var (chunk, reference) = chunkRefPairs.Dequeue();
                
                // Try to add the reference to visited refs. Continue if already present.
                if (!visitedReferences.Add(reference))
                    continue;
                
                // Decode chunk.
                var decodedChunk = chunk.Decode(reference, hasher);
                
                // Skip iteration on data chunks.
                if (decodedChunk.IsDataChunk)
                    continue;
                
                // If intermediate chunk, extract child references.
                var childReferences = ((SwarmDecodedIntermediateCac)decodedChunk).ChildReferences;
                    
                // Run fetch and recover with parity asynchronously.
                var decoder = new ChunkParityDecoder(childReferences, chunkStore);
                await decoder.TryFetchAndRecoverAsync(
                    redundancyStrategy,
                    redundancyStrategyFallback,
                    forceFetchAllChunks: true,
                    forceRecoverParities: includeParities,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                
                // Try to iterate on child chunks from decoder.
                foreach (var childReference in childReferences.Where(r => !r.IsParity || includeParities))
                {
                    // Try to get the chunk.
                    var childChunk = decoder.TryGetChunk(childReference.Reference.Hash);
                    if (childChunk == null)
                    {
                        await onChunkNotFound(childReference).ConfigureAwait(false);
                    }
                    else
                    {
                        await onChunkFound(childChunk, childReference).ConfigureAwait(false);
                        chunkRefPairs.Enqueue((childChunk, childReference.Reference));
                    }
                }
            }
        }
        
        private async Task TraverseDataHelperAsync(
            SwarmReference rootReference,
            HashSet<SwarmReference> visitedReferences,
            OnChunkFoundAsync onChunkFound,
            OnChunkNotFoundAsync onChunkNotFound,
            RedundancyLevel redundancyLevel,
            RedundancyStrategy redundancyStrategy, 
            bool redundancyStrategyFallback,
            bool includeParities,
            CancellationToken cancellationToken)
        {
            // Try resolve root chunk from reference.
            var rootChunk = await TryGetChunkAsync(
                rootReference.Hash,
                redundancyLevel,
                cancellationToken).ConfigureAwait(false);
            var rootShardReference = new SwarmShardReference(rootReference, false);
            if (rootChunk == null)
            {
                await onChunkNotFound(rootShardReference).ConfigureAwait(false);
                return;
            }
            await onChunkFound(rootChunk, rootShardReference).ConfigureAwait(false);
            
            // A single owner chunk is the whole traversal, as bee does: don't enter the wrapped chunk.
            if (rootChunk is not SwarmCac rootCac)
                return;
            
            await TraverseDataFromChunkHelperAsync(
                rootCac,
                rootReference,
                visitedReferences,
                onChunkFound,
                onChunkNotFound,
                redundancyStrategy,
                redundancyStrategyFallback,
                includeParities,
                cancellationToken).ConfigureAwait(false);
        }
        
        private async Task TraverseMantarayNodeFromDecodedHelperAsync(
            ReferencedMantarayNode manifestNode,
            HashSet<SwarmReference> visitedReferences,
            OnChunkFoundAsync onChunkFound,
            OnInvalidChunkFoundAsync onInvalidChunkFound,
            OnChunkNotFoundAsync onChunkNotFound,
            RedundancyLevel redundancyLevel,
            RedundancyStrategy redundancyStrategy, 
            bool redundancyStrategyFallback,
            bool includeParities,
            CancellationToken cancellationToken)
        {
            // Traverse forks.
            foreach (var fork in manifestNode.Forks.Values)
            {
                //skip already visited chunks
                if (visitedReferences.Contains(fork.Node.Reference))
                    continue;
                
                await TraverseMantarayNodeHelperAsync(
                    fork.Node.Reference,
                    fork.Node.Metadata,
                    fork.Node.NodeTypeFlags,
                    visitedReferences,
                    onChunkFound,
                    onInvalidChunkFound,
                    onChunkNotFound,
                    redundancyLevel,
                    redundancyStrategy,
                    redundancyStrategyFallback,
                    includeParities,
                    cancellationToken).ConfigureAwait(false);
            }
            
            // Traverse data.
            if (manifestNode.EntryReference.HasValue &&
                !SwarmReference.IsZero(manifestNode.EntryReference.Value) &&
                !visitedReferences.Contains(manifestNode.EntryReference.Value)) //skip already visited chunks
                await TraverseDataHelperAsync(
                    manifestNode.EntryReference.Value,
                    visitedReferences,
                    onChunkFound,
                    onChunkNotFound,
                    redundancyLevel,
                    redundancyStrategy,
                    redundancyStrategyFallback,
                    includeParities,
                    cancellationToken).ConfigureAwait(false);
        }
        
        private async Task TraverseMantarayNodeHelperAsync(
            SwarmReference reference,
            IReadOnlyDictionary<string, string>? metadata,
            NodeType nodeTypeFlags,
            HashSet<SwarmReference> visitedReferences,
            OnChunkFoundAsync onChunkFound,
            OnInvalidChunkFoundAsync onInvalidChunkFound,
            OnChunkNotFoundAsync onChunkNotFound,
            RedundancyLevel redundancyLevel,
            RedundancyStrategy redundancyStrategy, 
            bool redundancyStrategyFallback,
            bool includeParities,
            CancellationToken cancellationToken)
        {
            visitedReferences.Add(reference);
            
            // Resolve node chunk.
            var nodeChunk = await TryGetChunkAsync(
                reference.Hash,
                redundancyLevel,
                cancellationToken).ConfigureAwait(false);
            var nodeShardReference = new SwarmShardReference(reference, false);
            if (nodeChunk == null)
            {
                await onChunkNotFound(nodeShardReference).ConfigureAwait(false);
                return;
            }
            if (nodeChunk is not SwarmCac nodeCac) //soc is not a manifest node
            {
                await onInvalidChunkFound(nodeChunk, nodeShardReference).ConfigureAwait(false);
                return;
            }
            await onChunkFound(nodeCac, nodeShardReference).ConfigureAwait(false);
            
            // Build and decode node.
            var manifestNode = new ReferencedMantarayNode(
                nodeCac,
                reference,
                chunkStore,
                redundancyStrategy,
                redundancyStrategyFallback,
                metadata,
                nodeTypeFlags);
            manifestNode.DecodeFromChunk();
            
            await TraverseMantarayNodeFromDecodedHelperAsync(
                manifestNode,
                visitedReferences,
                onChunkFound,
                onInvalidChunkFound,
                onChunkNotFound,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback,
                includeParities,
                cancellationToken).ConfigureAwait(false);
        }
        
        private Task<SwarmChunk?> TryGetChunkAsync(
            SwarmHash hash,
            RedundancyLevel redundancyLevel,
            CancellationToken cancellationToken)
        {
            // Use chunk replica resolver if required.
            var leveledChunkStore = redundancyLevel == RedundancyLevel.None ?
                chunkStore :
                new ReplicaResolverChunkStore(chunkStore, redundancyLevel, new Hasher());
            
            return leveledChunkStore.TryGetAsync(hash, cancellationToken);
        }
    }
}