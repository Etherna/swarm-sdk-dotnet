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

    internal sealed class ChunkParityGenerator(
        RedundancyLevel redundancyLevel,
        bool encryptChunks,
        ChunkBmtPipelineStage shortBmtPipelineStage)
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
        
        // Fields.
        private readonly List<ShardsBufferLevel> bufferLevels = [];

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
            var reedSolomonEncoder = ReedSolomon.NET.ReedSolomon.Create(bufferLevel.DataShardsAmount, parities);
            reedSolomonEncoder.EncodeParity(bufferLevel.Buffer[..totalShards], 0, SwarmCac.SpanDataSize);

            // Report parity chunks.
            for (var i = bufferLevel.DataShardsAmount; i < totalShards; i++)
            {
                var spanData = bufferLevel.Buffer[i];
                var span = spanData[..SwarmCac.SpanSize];

                var args = new HasherPipelineFeedArgs(swarmChunkBmt, span, spanData, cancellationToken: cancellationToken);
                await shortBmtPipelineStage.FeedAsync(args).ConfigureAwait(false);

                await addParityChunkCallback(
                    chunkLevel,
                    new SwarmChunkHeader(
                        args.Reference!.Value,
                        span,
                        true),
                    swarmChunkBmt,
                    cancellationToken).ConfigureAwait(false);
            }
            
            // Clear current level to reuse.
            bufferLevel.Clear();
        }
    }
}