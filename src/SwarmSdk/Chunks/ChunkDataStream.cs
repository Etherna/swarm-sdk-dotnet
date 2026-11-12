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
using Etherna.SwarmSdk.Extensions;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Chunks
{
    public sealed class ChunkDataStream : Stream
    {
        // Fields.
        private readonly SwarmDecodedCacBase decodedRoot;
        private readonly Hasher hasher = new();
        private List<Dictionary<SwarmReference, SwarmDecodedCacBase>> levelsCache = [];
        private readonly uint maxDataSegmentsInChunk;
        
        private long _position;

        // Constructor.
        private ChunkDataStream(
            SwarmDecodedCacBase decodedRoot,
            IReadOnlyChunkStore chunkStore,
            RedundancyStrategy redundancyStrategy,
            bool redundancyStrategyFallback)
        {
            ChunkStore = chunkStore;
            this.decodedRoot = decodedRoot;
            Position = 0;

            // Define segments info.
            if (decodedRoot.RedundancyLevel == RedundancyLevel.None)
            {
                maxDataSegmentsInChunk = SwarmCac.DataSize / (uint)decodedRoot.Reference.Size;

                // If root chunk has no redundancy, strategy is kept to None or set to Data, and fallback is disabled.
                RedundancyStrategy = redundancyStrategy == RedundancyStrategy.None ?
                    RedundancyStrategy.None : RedundancyStrategy.Data;
                RedundancyStrategyFallback = false;
            }
            else
            {
                maxDataSegmentsInChunk = (uint)decodedRoot.RedundancyLevel.GetMaxDataShards(
                    decodedRoot.Reference.IsEncrypted);
                
                // If root chunk has redundancy, and if strategy has fallback, upgrade strategy from None to Data or
                // keep it unchanged. (fallback == true && Strategy == None) is not a valid configuration.
                RedundancyStrategy = redundancyStrategyFallback && redundancyStrategy == RedundancyStrategy.None ?
                    RedundancyStrategy.Data : redundancyStrategy;
                RedundancyStrategyFallback = redundancyStrategyFallback;
            }
        }

        // Static builder.
        public static async Task<ChunkDataStream> BuildNewAsync(
            SwarmReference reference,
            IReadOnlyChunkStore chunkStore,
            RedundancyLevel redundancyLevel,
            RedundancyStrategy redundancyStrategy, 
            bool redundancyStrategyFallback)
        {
            ArgumentNullException.ThrowIfNull(chunkStore);
            
            // Use chunk redundancy resolver if required.
            var rootChunkStore = redundancyLevel == RedundancyLevel.None ?
                chunkStore :
                new ReplicaResolverChunkStore(chunkStore, redundancyLevel, new Hasher());
            
            // Resolve root chunk.
            var rootChunk = await rootChunkStore.GetAsync(reference.Hash).ConfigureAwait(false);
            if (rootChunk is not SwarmCac rootCac) //soc is not supported
                throw new SwarmChunkTypeException(rootChunk, $"Chunk {reference} is not a Content Addressed Chunk.");
            
            return BuildNew(
                rootCac,
                reference,
                chunkStore,
                redundancyStrategy,
                redundancyStrategyFallback);
        }

        public static ChunkDataStream BuildNew(
            SwarmCac rootChunk,
            IReadOnlyChunkStore chunkStore,
            RedundancyStrategy redundancyStrategy, 
            bool redundancyStrategyFallback,
            EncryptionKey256? encryptionKey = null)
        {
            ArgumentNullException.ThrowIfNull(rootChunk);
            return BuildNew(
                rootChunk,
                new SwarmReference(rootChunk.Hash, encryptionKey),
                chunkStore,
                redundancyStrategy,
                redundancyStrategyFallback);
        }
        
        public static ChunkDataStream BuildNew(
            SwarmCac rootChunk,
            SwarmReference reference,
            IReadOnlyChunkStore chunkStore,
            RedundancyStrategy redundancyStrategy, 
            bool redundancyStrategyFallback)
        {
            ArgumentNullException.ThrowIfNull(rootChunk);
            return new ChunkDataStream(
                rootChunk.Decode(reference, new Hasher()),
                chunkStore,
                redundancyStrategy,
                redundancyStrategyFallback);
        }

        // Properties.
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public IReadOnlyChunkStore ChunkStore { get; }
        public override long Length => (long)decodedRoot.SpanLength;
        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Cannot set position before the beginning of the stream");
                if (value > Length)
                    throw new ArgumentOutOfRangeException(nameof(value), "Cannot set position past the end of the stream");
                
                _position = value;
            }
        }
        public RedundancyStrategy RedundancyStrategy { get; }
        public bool RedundancyStrategyFallback { get; }
        public SwarmReference Reference => decodedRoot.Reference;

        // Methods.
        public override void Flush() { }
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            var task = ReadAsync(buffer, offset, count);
            task.Wait();
            return task.Result;
        }
        
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Return 0 when the stream is at the end.
            var dataToRead = (int)Math.Min(buffer.Length, Length - Position);
            if (dataToRead == 0)
                return 0;
            
            // Read data from chunks.
            await CopyDataToBufferAsync(buffer[..dataToRead], cancellationToken).ConfigureAwait(false);
            
            Position += dataToRead;
            return dataToRead;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            Position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
            };

        public override void SetLength(long value) => throw new NotSupportedException();

        public async Task<byte[]> ToByteArrayAsync()
        {
            using var ms = new MemoryStream();
            Position = 0;
            await CopyToAsync(ms).ConfigureAwait(false);
            return ms.ToArray();
        }

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        
        // Private helpers.
        private async Task CopyDataToBufferAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            List<Dictionary<SwarmReference, SwarmDecodedCacBase>> newLevelsCache = [[]];
            
            // Init with root level info.
            var levelStartDataOffset = Position;
            var levelEndDataOffset = Length - (Position + buffer.Length);
            SwarmDecodedCacBase[] levelDecodedChunks = [decodedRoot];
            
            // Reuse these for memory optimization.
            /*
             * When chunks have parities, these needs to be resolved with a ChunkParityDecoder, that will try
             * to fetch and recover child chunks.
             * Each decoder is responsible for decoding a single chunk, so we need a list of decoders for each level.
             * Associated with each decoder there is also a list of references to explore, resolvable from that
             * specific decoder.
             */
            Dictionary<SwarmReference, SwarmDecodedCacBase> decodedChunksMap = [];
            List<Task> fetchAndRecoverTasks = [];
            List<SwarmReference> nextLevelReferences = [];
            Dictionary<SwarmReference, ulong> impliedSpanLengths = [];
            List<ChunkParityDecoder> parityDecoders = [];

            // Iterate on all levels from root to data chunks. Terminate when no chunks remain.
            for (var levelIndex = 0;; levelIndex++)
            {
                // Init level.
                IEnumerable<SwarmReference> referencesToCache = [];
                
                decodedChunksMap.Clear();
                fetchAndRecoverTasks.Clear();
                nextLevelReferences.Clear();
                parityDecoders.Clear();
                impliedSpanLengths.Clear();
                
                // Optimize value search for chunks in level.
                // Analyzing from right, it can only be monotonically non-decreasing.
                long dataSizeBySegment = SwarmCac.DataSize;

                // Parse all chunks on current level. Start from end to copy data first, if present in level.
                for (int chunkIndex = levelDecodedChunks.Length - 1; chunkIndex >= 0; chunkIndex--)
                {
                    // Get decoded chunk info.
                    var decodedChunk = levelDecodedChunks[chunkIndex];
                    var spanLength = (long)decodedChunk.SpanLength;

                    var isFirstChunkInLevel = chunkIndex == 0;
                    var isLastChunkInLevel = chunkIndex == levelDecodedChunks.Length - 1;

                    // If is a data chunk, report data on buffer and update bounds. Then continue.
                    if (decodedChunk.IsDataChunk)
                    {
                        var data = ((SwarmDecodedDataCac)decodedChunk).Data;
                        
                        //check end offset consistency
                        var dataToCopyStart = isFirstChunkInLevel ? (int)levelStartDataOffset : 0;
                        var dataToCopySize = data.Length - dataToCopyStart - (int)levelEndDataOffset;
                        if (dataToCopySize <= 0)
                            throw new InvalidOperationException("Invalid data to copy size");

                        //copy data to end of buffer, and shrink buffer
                        data[dataToCopyStart..(dataToCopyStart + dataToCopySize)].CopyTo(buffer[^dataToCopySize..]);
                        buffer = buffer[..^dataToCopySize];

                        //update level bounds
                        levelEndDataOffset = 0;

                        continue;
                    }

                    // Extract references.
                    var intermediateChunk = (SwarmDecodedIntermediateCac)decodedChunk;
                    var childReferences = intermediateChunk.ChildReferences;

                    // Find referred data size by data reference.
                    var dataReferencesAmount = childReferences.TakeWhile(r => !r.IsParity).Count();

                    // Each data child must hold the span its parent implies for it, or the tree is malformed.
                    for (var i = 0; i < dataReferencesAmount; i++)
                        impliedSpanLengths[childReferences[i].Reference] = intermediateChunk.DataReferenceSpanLengths[i];
                    while (dataSizeBySegment * dataReferencesAmount < spanLength)
                        dataSizeBySegment *= maxDataSegmentsInChunk;

                    // Define chunk's data references to read and set bounds for the next level.
                    var startDataReferenceToSkip = 0;
                    var endDataReferencesToSkip = 0;
                    if (isFirstChunkInLevel)
                    {
                        startDataReferenceToSkip = (int)(levelStartDataOffset / dataSizeBySegment);
                        levelStartDataOffset -= startDataReferenceToSkip * dataSizeBySegment;
                    }
                    if (isLastChunkInLevel)
                    {
                        var lastPartialSegmentDataSize = spanLength % dataSizeBySegment;
                        if (levelEndDataOffset >= lastPartialSegmentDataSize)
                        {
                            endDataReferencesToSkip = (int)((levelEndDataOffset - lastPartialSegmentDataSize) / dataSizeBySegment);
                            if (lastPartialSegmentDataSize > 0)
                                endDataReferencesToSkip++;
                        }
                        if (endDataReferencesToSkip > 0)
                            levelEndDataOffset -= spanLength % dataSizeBySegment == 0
                                ? endDataReferencesToSkip * dataSizeBySegment
                                : (endDataReferencesToSkip - 1) * dataSizeBySegment + spanLength % dataSizeBySegment;
                    }
                    
                    // Identify required child references.
                    var requiredChildReferences = childReferences
                        .TakeWhile(r => !r.IsParity)
                        .Select(r => r.Reference)
                        .Skip(startDataReferenceToSkip)
                        .SkipLast(endDataReferencesToSkip)
                        .ToArray();
                    nextLevelReferences.InsertRange(0, requiredChildReferences);

                    // If is the last chunk on the level, evaluate to cache the chunk and its right brothers.
                    if (isLastChunkInLevel)
                    {
                        if (endDataReferencesToSkip > 0)
                            newLevelsCache[levelIndex].Add(decodedChunk.Reference, decodedChunk);

                        referencesToCache = RedundancyStrategy == RedundancyStrategy.None ? [] :
                            childReferences
                                .TakeWhile(r => !r.IsParity)
                                .Select(r => r.Reference)
                                .TakeLast(endDataReferencesToSkip);
                    }
                    
                    // If cache contains all required references, continue.
                    if (levelsCache.Count > levelIndex + 1 &&
                        requiredChildReferences.All(r => levelsCache[levelIndex + 1].ContainsKey(r)))
                        continue;
                    
                    // Run fetch and recover with parity asynchronously.
                    var decoder = new ChunkParityDecoder(childReferences, ChunkStore);
                    if (RedundancyStrategy == RedundancyStrategy.None)
                    {
                        fetchAndRecoverTasks.Add(decoder.FetchWithoutStrategyAsync(
                            requiredChildReferences,
                            cancellationToken: cancellationToken));
                    }
                    else
                    {
                        fetchAndRecoverTasks.Add(decoder.FetchAndRecoverAsync(
                            RedundancyStrategy,
                            RedundancyStrategyFallback,
                            cancellationToken: cancellationToken));
                    }

                    parityDecoders.Add(decoder);
                }

                // Wait all "fetch and recover" tasks for this level.
                await Task.WhenAll(fetchAndRecoverTasks).ConfigureAwait(false);
                
                // If next level is empty, set cache and return.
                if (nextLevelReferences.Count == 0)
                {
                    // Verify the full buffer has been written.
                    if (buffer.Length != 0)
                        throw new InvalidOperationException("Not full buffer has been written");

                    // Replace cache.
                    levelsCache = newLevelsCache;

                    return;
                }

                // Get and decode CACs from parity decoders.
                foreach (var decodedChunk in parityDecoders.SelectMany(
                             decoder => decoder.ShardReferences
                                 .TakeWhile(s => !s.IsParity)
                                 .Select(s => (s.Reference, Chunk: decoder.TryGetChunk(s.Reference.Hash)))
                                 .Where(p => p.Chunk != null)
                                 .Select(p => p.Chunk!.Decode(p.Reference, hasher))))
                {
                    if (impliedSpanLengths.TryGetValue(decodedChunk.Reference, out var impliedSpanLength) &&
                        decodedChunk.SpanLength != impliedSpanLength)
                        throw new SwarmChunkFormatException(
                            decodedChunk.Reference.Hash,
                            $"Chunk {decodedChunk.Reference.Hash} span of {decodedChunk.SpanLength} bytes is not the {impliedSpanLength} bytes its parent implies");
                    decodedChunksMap.Add(decodedChunk.Reference, decodedChunk);
                }
                
                // Copy decoded chunks to new level cache.
                newLevelsCache.Add([]);
                foreach (var reference in referencesToCache)
                {
                    if (decodedChunksMap.TryGetValue(reference, out var chunk))
                        newLevelsCache[levelIndex + 1][reference] = chunk;
                    else
                        newLevelsCache[levelIndex + 1][reference] = levelsCache[levelIndex + 1][reference];
                }

                // Set new level decoded chunks in order.
                levelDecodedChunks = nextLevelReferences
                    .Select(r => decodedChunksMap.TryGetValue(r, out var chunk) ?
                        chunk : levelsCache[levelIndex + 1][r])
                    .ToArray();
            }
        }
    }
}