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
using Etherna.SwarmSdk.Stores;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Chunks
{
    /// <summary>
    /// Tries to recover data children of an intermediate chunk using redundancy.
    /// Class is not thread-safe, execute Fetch and Recovery in sequence.
    /// </summary>
    public sealed class ChunkParityDecoder
    {
        // Consts.
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        // Fields.
        private readonly IReadOnlyChunkStore chunkStore;
        private readonly Dictionary<SwarmHash, int> hashIndexMap = new();

        private readonly SwarmShardReference[] _shardReferences;
        private readonly byte[][] shardsBuffer; //byte[i] could be null when relative chunk is not present

        private readonly int dataShardsAmount;

        // Constructor.
        public ChunkParityDecoder(
            ReadOnlySpan<byte> plainSpanData,
            bool encryptedDataReferences,
            IReadOnlyChunkStore chunkStore)
            : this(SwarmCac.GetIntermediateReferencesFromSpanData(
                    plainSpanData[..SwarmCac.SpanSize],
                    plainSpanData[SwarmCac.SpanSize..],
                    encryptedDataReferences),
                chunkStore)
        { }

        public ChunkParityDecoder(
            IEnumerable<SwarmShardReference> shardReferences,
            IReadOnlyChunkStore chunkStore)
        {
            ArgumentNullException.ThrowIfNull(shardReferences);

            this.chunkStore = chunkStore;
            _shardReferences = shardReferences.ToArray();
            shardsBuffer = new byte[_shardReferences.Length][];
            dataShardsAmount = _shardReferences.Count(r => !r.IsParity);

            for (var i = 0; i < _shardReferences.Length; i++)
                hashIndexMap[_shardReferences[i].Reference.Hash] = i;
        }

        // Properties.
        public bool AreDataChunksReady => shardsBuffer.Take(dataShardsAmount).All(s => s != null!);
        public bool IsRecoveryPerformed { get; private set; }
        public IReadOnlyList<SwarmShardReference> ShardReferences => _shardReferences;

        // Methods.
        public async Task AddChunksToStoreAsync(
            IChunkStore destinationStore,
            IEnumerable<SwarmHash> hashes,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(destinationStore);
            ArgumentNullException.ThrowIfNull(hashes);

            foreach (var hash in hashes)
            {
                var chunk = GetChunk(hash);
                await destinationStore.AddAsync(chunk, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task FetchAndRecoverAsync(
            RedundancyStrategy firstStrategy,
            bool strategyFallback,
            TimeSpan? customStrategyTimeout = null,
            bool forceFetchAllChunks = false,
            bool forceRecoverParities = false,
            CancellationToken cancellationToken = default)
        {
            // Verify if recovery has already been completed with success.
            if (IsRecoveryPerformed)
                return;

            // Run fetch and proceed if succeeded.
            await FetchChunksAsync(
                firstStrategy,
                strategyFallback,
                customStrategyTimeout,
                forceFetchAllChunks,
                cancellationToken).ConfigureAwait(false);

            // Run recovery.
            RecoverChunks(forceRecoverParities);
        }

        /// <summary>
        /// Retrieves missing chunks to perform redundancy recovery.
        /// </summary>
        /// <param name="firstStrategy">First strategy to try. None is not allowed</param>
        /// <param name="strategyFallback">When true and a strategy fails, fallback to try the next</param>
        /// <param name="customStrategyTimeout">Optional custom timeout for each strategy</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task FetchChunksAsync(
            RedundancyStrategy firstStrategy,
            bool strategyFallback,
            TimeSpan? customStrategyTimeout = null,
            bool forceFetchAllChunks = false,
            CancellationToken cancellationToken = default)
        {
            // Verify if data chunks are already available.
            if (AreDataChunksReady)
                return;

            // Init chunk fetch tasks list with already found chunks.
            var fetchResults = shardsBuffer.Select(b => (bool?)(b != null! ? true : null)).ToArray();

            // Run redundancy strategies.
            var succeeded = false;
            for (int s = (int)firstStrategy; !succeeded && s <= (int)RedundancyStrategy.Race; s++)
            {
                var strategy = (RedundancyStrategy)s;

                succeeded = await TryRunStrategyAsync(
                    strategy,
                    fetchResults,
                    customStrategyTimeout,
                    forceFetchAllChunks,
                    cancellationToken).ConfigureAwait(false);
                if (!strategyFallback)
                    break;
            }

            if (!succeeded)
                throw new KeyNotFoundException();
        }

        public async Task FetchWithoutStrategyAsync(
            SwarmReference[] fetchReferences,
            CancellationToken cancellationToken)
        {
            // Verify that fetching references are contained into shard references.
            if (fetchReferences.Except(_shardReferences.Select(r => r.Reference)).Any())
                throw new ArgumentException("Not all fetching references are contained into shard references");
            
            // Verify if required chunks are already available.
            var hashesToQuery = fetchReferences.Where(r => shardsBuffer[hashIndexMap[r.Hash]] == null!)
                .Select(r => r.Hash)
                .ToArray();
            if (hashesToQuery.Length == 0)
                return;

            // Fetch chunks.
            var results = await chunkStore.GetAsync(
                hashesToQuery,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            
            // Verify results.
            var missingChunks = false;
            foreach (var hash in hashesToQuery)
            {
                if (!results.TryGetValue(hash, out var chunk))
                    missingChunks = true;

                if (chunk != null)
                {
                    // Store found chunk. Pad data with zeros if it is smaller than SpanDataSize.
                    var spanData = new byte[SwarmCac.SpanDataSize];
                    chunk.GetFullPayload().CopyTo(spanData);
                    shardsBuffer[hashIndexMap[hash]] = spanData;
                }
                else
                    missingChunks = true;
            }

            if (missingChunks)
                throw new KeyNotFoundException();
        }

        public SwarmCac GetChunk(SwarmHash hash) =>
            TryGetChunk(hash) ??
            throw new KeyNotFoundException("Chunk not available. Ensure fetch and recover have succeeded");

        /// <summary>
        /// Get all missing shards, from both data and parities
        /// </summary>
        public SwarmShardReference[] GetMissingShards() =>
            shardsBuffer
                .Zip(_shardReferences)
                .Where(zip => zip.First == null!)
                .Select(zip => zip.Second).ToArray();

        public bool HasChunk(SwarmHash hash)
        {
            if (!hashIndexMap.TryGetValue(hash, out var i))
                return false;
            return shardsBuffer[i] != null!;
        }

        /// <summary>
        /// Perform redundancy recovery.
        /// </summary>
        /// <param name="forceRecoverParities">When false skip recovery if all data chunks are present</param>
        public void RecoverChunks(bool forceRecoverParities)
        {
            // Verify if recovery has already been completed with success.
            if (IsRecoveryPerformed)
                return;

            // Verify if there are enough recovered shards.
            if (shardsBuffer.Count(b => b != null!) < dataShardsAmount)
                throw new InvalidOperationException("Not enough fetched shards");

            // If all data shards are not null, recovery is not needed.
            if (!forceRecoverParities && AreDataChunksReady)
                return;

            // Run actual recovery. Use Reed-Solomon erasure coding decoder to recover data shards.
            var reedSolomonEncoder = ReedSolomon.NET.ReedSolomon.Create(
                dataShardsAmount,
                shardsBuffer.Length - dataShardsAmount);

            var shardsPresent = new bool[shardsBuffer.Length];

            try
            {
                for (int i = 0; i < shardsBuffer.Length; i++)
                {
                    if (shardsBuffer[i] == null!)
                        shardsBuffer[i] = new byte[SwarmCac.SpanDataSize];
                    else
                        shardsPresent[i] = true;
                }

                //decode missing shards
                reedSolomonEncoder.DecodeMissing(shardsBuffer, shardsPresent, 0, SwarmCac.SpanDataSize);
            }
            catch
            {
                // Restore missing chunks in buffer in case of error.
                for (int i = 0; i < shardsBuffer.Length; i++)
                    if (!shardsPresent[i])
                        shardsBuffer[i] = null!;

                throw;
            }

            // Return as succeeded.
            IsRecoveryPerformed = true;
        }

        public async Task<bool> TryFetchAndRecoverAsync(
            RedundancyStrategy firstStrategy,
            bool strategyFallback,
            TimeSpan? customStrategyTimeout = null,
            bool forceFetchAllChunks = false,
            bool forceRecoverParities = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await FetchAndRecoverAsync(
                    firstStrategy,
                    strategyFallback,
                    customStrategyTimeout,
                    forceFetchAllChunks,
                    forceRecoverParities,
                    cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Try to retrieve enough missing chunks to perform redundancy recovery.
        /// </summary>
        /// <param name="firstStrategy">First strategy to try. None is not allowed</param>
        /// <param name="strategyFallback">When true and a strategy fails, fallback to try the next</param>
        /// <param name="customStrategyTimeout">Optional custom timeout for each strategy</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Succeeded recovery</returns>
        public async Task<bool> TryFetchChunksAsync(
            RedundancyStrategy firstStrategy,
            bool strategyFallback,
            TimeSpan? customStrategyTimeout = null,
            bool forceFetchAllChunks = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await FetchChunksAsync(
                    firstStrategy,
                    strategyFallback,
                    customStrategyTimeout,
                    forceFetchAllChunks,
                    cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        public SwarmCac? TryGetChunk(SwarmHash hash)
        {
            if (!hashIndexMap.TryGetValue(hash, out var i) ||
                shardsBuffer[i] == null!)
                return null;

            // If is not last chunk, or if is encrypted, simply read from buffer.
            if (i != dataShardsAmount - 1 || _shardReferences[i].Reference.IsEncrypted)
                return new SwarmCac(hash, shardsBuffer[i]);

            // Else, if is last chunk and not encrypted, try to remove possible padding.
            ReadOnlySpan<byte> span = shardsBuffer[i].AsSpan()[..SwarmCac.SpanSize];
            var spanLength = SwarmCac.DecodedSpanToLength(SwarmCac.IsSpanEncoded(span) ? SwarmCac.DecodeSpan(span) : span);
            var redundancyLevel = SwarmCac.SpanToRedundancyLevel(span);

            return new SwarmCac(hash, shardsBuffer[i].AsMemory(0,
                SwarmCac.SpanSize + SwarmCac.CalculatePlainDataLength(spanLength, redundancyLevel, false)));
        }

        /// <summary>
        /// Try to perform redundancy recovery.
        /// </summary>
        /// <param name="forceRecoverParities">When false skip recovery if all data chunks are present</param>
        /// <returns>Succeeded recovery</returns>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        public bool TryRecoverChunks(bool forceRecoverParities)
        {
            try
            {
                RecoverChunks(forceRecoverParities);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Helpers.

        /// <summary>
        /// Common goal is to fetch at least as many chunks as the number of data shards.
        /// DATA strategy has a max error tolerance of zero.
        /// RACE strategy has a max error tolerance of number of parity chunks.
        /// </summary>
        /// <returns>Succeeded result</returns>
        private async Task<bool> TryRunStrategyAsync(
            RedundancyStrategy strategy,
            bool?[] fetchResults,
            TimeSpan? customStrategyTimeout,
            bool forceFetchAllChunks,
            CancellationToken cancellationToken)
        {
            // Define allowed errors and hashes to query, based on strategy.
            var allowedErrors = 0;
            SwarmHash[] hashesToQuery;

            switch (strategy)
            {
                case RedundancyStrategy.None:
                    throw new ArgumentException($"Strategy None is not allowed here", nameof(strategy));

                case RedundancyStrategy.Data: //only retrieve data shards
                    hashesToQuery = fetchResults
                        .Zip(_shardReferences.Where(sr => !sr.IsParity))
                        .Where(zip => zip.First == null)
                        .Select(zip => zip.Second.Reference.Hash)
                        .ToArray();
                    break;

                case RedundancyStrategy.Prox: //proximity driven selective fetching
                    return false; //TODO: strategy not implemented

                case RedundancyStrategy.Race:
                    allowedErrors = _shardReferences.Length - dataShardsAmount;
                    hashesToQuery = fetchResults
                        .Zip(_shardReferences)
                        .Where(zip => zip.First == null)
                        .Select(zip => zip.Second.Reference.Hash)
                        .ToArray();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null);
            }

            // Define and verify requirements.
            var succeededFetches = 0;
            var failedFetches = 0;
            foreach (var result in fetchResults)
            {
                switch (result)
                {
                    case true: succeededFetches++; break;
                    case false: failedFetches++; break;
                }
            }
            if (hashesToQuery.Length == 0)
                return succeededFetches >= dataShardsAmount;
            if (succeededFetches >= dataShardsAmount)
                return true;
            if (!forceFetchAllChunks && failedFetches > allowedErrors)
                return false;

            // Run chunks query.
            using var timeoutCts = new CancellationTokenSource(customStrategyTimeout ?? DefaultTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var results = await chunkStore.GetAsync(
                hashesToQuery,
                canReturnAfterFailed: forceFetchAllChunks ? null : allowedErrors + 1,
                canReturnAfterSucceeded: dataShardsAmount - succeededFetches,
                cancellationToken: linkedCts.Token).ConfigureAwait(false);

            // Verify results.
            foreach (var hash in hashesToQuery)
            {
                if (!results.TryGetValue(hash, out var chunk))
                    continue;

                var shardIndex = hashIndexMap[hash];
                if (chunk is SwarmCac) //validate chunk is not null or SOC
                {
                    // Store found chunk. Pad data with zeros if it is smaller than SpanDataSize.
                    var spanData = new byte[SwarmCac.SpanDataSize];
                    chunk.GetFullPayload().CopyTo(spanData);
                    shardsBuffer[shardIndex] = spanData;

                    // Update results.
                    fetchResults[shardIndex] = true;
                    succeededFetches++;
                }
                else
                {
                    // Update results.
                    fetchResults[shardIndex] = false;
                    failedFetches++;
                }
            }

            return succeededFetches >= dataShardsAmount;
        }
    }
}