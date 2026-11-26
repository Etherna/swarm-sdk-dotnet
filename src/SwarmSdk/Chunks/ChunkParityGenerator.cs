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
using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Chunks
{
    internal delegate Task AddChunkToLevel(
        int level,
        SwarmChunkHeader chunkHeader,
        SwarmChunkBmt swarmChunkBmt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Generate the parity chunks of each level of a chunk tree. A full level is encoded and its parities are hashed
    /// on up to <paramref name="parallelism"/> threads, while the stage feeding it waits: the parities enter the level
    /// in their order once all are hashed
    /// </summary>
    /// <param name="redundancyLevel">The redundancy level</param>
    /// <param name="encryptChunks">True if the data chunks are referenced with their key</param>
    /// <param name="shortBmtPipelineStage">The stage hashing and storing a parity chunk</param>
    /// <param name="parallelism">The threads a level is encoded and hashed on, as the chunk concurrency of the
    /// pipeline</param>
    internal sealed class ChunkParityGenerator(
        RedundancyLevel redundancyLevel,
        bool encryptChunks,
        ChunkBmtPipelineStage shortBmtPipelineStage,
        int parallelism)
    {
        // Internal classes.
        private class ShardsBufferLevel
        {
            // Properties.
            public byte[][] Buffer { get; } = new byte[SwarmChunkBmt.SegmentsCount][];
            public int DataShardsAmount { get; private set; }
            public int[] OriginalLengths { get; } = new int[SwarmChunkBmt.SegmentsCount];

            // Methods.
            public void AddNewChunk(byte[] spanData, int originalLength)
            {
                if (spanData.Length != SwarmCac.SpanDataSize)
                    throw new ArgumentException("Span data is not padded", nameof(spanData));
                
                Buffer[DataShardsAmount] = spanData;
                OriginalLengths[DataShardsAmount] = originalLength;
                DataShardsAmount++;
            }

            public void Clear() => DataShardsAmount = 0;
        }
        
        // Consts.
        private const int MinEncodingSliceSize = 256;

        // Fields.
        private readonly List<ShardsBufferLevel> bufferLevels = [];
        private readonly ConcurrentBag<SwarmChunkBmt> parityChunkBmts = [];

        // Properties.
        public bool EncryptChunks => encryptChunks;
        public int MaxChildrenChunks { get; } =
            redundancyLevel.GetMaxDataShards(encryptChunks) +
            redundancyLevel.GetParitiesAmount(encryptChunks, redundancyLevel.GetMaxDataShards(encryptChunks));
        
        /// <summary>
        /// Number of chunks after which the parity encode function should be called
        /// </summary>
        public int MaxDataShards { get; } = redundancyLevel.GetMaxDataShards(encryptChunks);
        
        public RedundancyLevel RedundancyLevel => redundancyLevel;

        // Methods.
        public int GetParitiesAmount(int shards) => redundancyLevel.GetParitiesAmount(encryptChunks, shards);

        /// <summary>
        /// Get the topmost chunk span data on levels.
        /// </summary>
        public ReadOnlyMemory<byte> GetRootSpanData()
        {
            if (RedundancyLevel == RedundancyLevel.None)
                throw new InvalidOperationException("Not using redundancy");
            if (bufferLevels.Count == 0)
                throw new InvalidOperationException("No chunks has been added");

            var lastBufferLevel = bufferLevels.Last();
            if (lastBufferLevel.DataShardsAmount == 0)
                throw new InvalidOperationException("Invalid empty level");

            return lastBufferLevel.Buffer[0].AsMemory()[..lastBufferLevel.OriginalLengths[0]];
        }

        /// <summary>
        /// Caches the chunk data on the given chunk level. If level is full, then encode chunks
        /// </summary>
        public async Task AddChunkToLevelAsync(
            int chunkLevel,
            ReadOnlyMemory<byte> spanData,
            AddChunkToLevel addParityChunkCallback,
            SwarmChunkBmt swarmChunkBmt,
            CancellationToken cancellationToken)
        {
            if (RedundancyLevel == RedundancyLevel.None)
                return;
            
            // Pad data.
            var originalLength = spanData.Length;
            if (spanData.Length != SwarmCac.SpanDataSize)
            {
                var newSpanData = new byte[SwarmCac.SpanDataSize];
                spanData.Span.CopyTo(newSpanData);
                spanData = newSpanData;
            }
            
            // Get level buffer.
            while (chunkLevel >= bufferLevels.Count)
                bufferLevels.Add(new ShardsBufferLevel());
            var bufferLevel = bufferLevels[chunkLevel];
            
            // Append chunk to the level buffer.
            bufferLevel.AddNewChunk(spanData.ToArray(), originalLength);
            
            // Add parity chunks if the level is full.
            if (bufferLevel.DataShardsAmount == MaxDataShards)
                await EncodeErasureDataAsync(
                    chunkLevel,
                    addParityChunkCallback,
                    swarmChunkBmt,
                    cancellationToken).ConfigureAwait(false);
        }

        public async Task ElevateCarrierChunkAsync(
            int chunkLevel,
            AddChunkToLevel addParityChunkCallback,
            SwarmChunkBmt swarmChunkBmt,
            CancellationToken cancellationToken)
        {
            if (RedundancyLevel == RedundancyLevel.None)
                return;
            
            var bufferLevel = bufferLevels[chunkLevel];
            if (bufferLevel.DataShardsAmount != 1)
                throw new InvalidOperationException("Cannot elevate carrier chunk because it is not the only one on level");

            await AddChunkToLevelAsync(
                chunkLevel + 1,
                bufferLevel.Buffer[0],
                addParityChunkCallback,
                swarmChunkBmt,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Produces and stores parity chunks
        /// </summary>
        public async Task EncodeErasureDataAsync(
            int chunkLevel,
            AddChunkToLevel addParityChunkCallback,
            SwarmChunkBmt swarmChunkBmt,
            CancellationToken cancellationToken)
        {
            if (RedundancyLevel == RedundancyLevel.None)
                return;
            
            var bufferLevel = bufferLevels[chunkLevel];
            if (bufferLevel.DataShardsAmount == 0)
                return;
            
            // Initialize parity shards.
            var parities = GetParitiesAmount(bufferLevel.DataShardsAmount);
            var totalShards = bufferLevel.DataShardsAmount + parities;
            for (var i = bufferLevel.DataShardsAmount; i < totalShards; i++)
                bufferLevel.Buffer[i] = new byte[SwarmCac.SpanDataSize];
            
            // Calculate parity chunks.
            //each byte column of the shards encodes on its own: slices of the columns encode in parallel
            var encoder = ReedSolomonEncoders.Get(bufferLevel.DataShardsAmount, parities);
            var shards = bufferLevel.Buffer[..totalShards];
            var slices = Math.Clamp(SwarmCac.SpanDataSize / MinEncodingSliceSize, 1, parallelism);
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = parallelism
            };
            Parallel.For(0, slices, parallelOptions, slice =>
            {
                var start = SwarmCac.SpanDataSize * slice / slices;
                var end = SwarmCac.SpanDataSize * (slice + 1) / slices;
                encoder.EncodeParity(shards, start, end - start);
            });

            // Hash parity chunks in parallel, each with a BMT of its own.
            var parityChunksArgs = new HasherPipelineFeedArgs[parities];
            await Parallel.ForAsync(0, parities, parallelOptions, async (parity, _) =>
            {
                if (!parityChunkBmts.TryTake(out var parityChunkBmt))
                    parityChunkBmt = new SwarmChunkBmt();
                try
                {
                    var spanData = bufferLevel.Buffer[bufferLevel.DataShardsAmount + parity];
                    var args = new HasherPipelineFeedArgs(
                        parityChunkBmt,
                        spanData.AsMemory(0, SwarmCac.SpanSize),
                        spanData,
                        cancellationToken: cancellationToken); //the token of the run, as every chunk write receives
                    await shortBmtPipelineStage.FeedAsync(args).ConfigureAwait(false);
                    parityChunksArgs[parity] = args;
                }
                finally
                {
                    parityChunkBmts.Add(parityChunkBmt);
                }
            }).ConfigureAwait(false);

            // Report parity chunks, in their order.
            foreach (var args in parityChunksArgs)
                await addParityChunkCallback(
                    chunkLevel,
                    new SwarmChunkHeader(
                        args.Reference!.Value,
                        args.Span,
                        true),
                    swarmChunkBmt,
                    cancellationToken).ConfigureAwait(false);
            
            // Clear current level to reuse.
            bufferLevel.Clear();
        }
    }
}