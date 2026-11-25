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
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Hashing.Pipeline
{
    /// <summary>
    /// Calculate hash of each chunk
    /// </summary>
    /// <param name="compactLevel">Number of hashes to try when searching for the best fist in buckets</param>
    /// <param name="isEncrypted">Encrypt chunk with random keys. If true, "KeysToTest = Max(compactLevel, 1)"</param>
    /// <param name="nextStage">Next pipeline stage</param>
    internal sealed class ChunkBmtPipelineStage(
        ushort compactLevel,
        bool isEncrypted,
        IHasherPipelineStage nextStage)
        : IHasherPipelineStage
    {
        // Private classes.
        private readonly struct EncryptedChunkResult
        {
            public SwarmReference Reference { init; get; }
            public ReadOnlyMemory<byte> SpanData { init; get; }
        }
        
        // Fields.
        private long _missedOptimisticHashing;

        // Dispose.
        public void Dispose()
        {
            nextStage.Dispose();
        }
        
        // Properties.
        public long MissedOptimisticHashing => _missedOptimisticHashing + nextStage.MissedOptimisticHashing;
        public IPostageStamper PostageStamper => nextStage.PostageStamper;

        // Methods.
        public async Task FeedAsync(HasherPipelineFeedArgs args)
        {
            if (args.SpanData.Length < SwarmCac.SpanSize)
                throw new InvalidOperationException("Data can't be shorter than span size here");
            if (args.SpanData.Length > SwarmCac.SpanDataSize)
                throw new InvalidOperationException("Data can't be longer than chunk + span size here");
            
            // Decide if encrypt chunk or not.
            if (compactLevel == 0 && !isEncrypted)
            {
                // If no chunk compaction or encryption are involved, simply calculate the hash and proceed.
                args.Reference = SwarmReference.FromSwarmHash(args.SwarmChunkBmt.Hash(args.SpanData));
                args.SwarmChunkBmt.Clear();
            }
            else
            {
                // Search best chunk key optimistically or simply encrypt. Use random keys when encrypting.
                var encryptionResult = compactLevel <= 1 ?
                    GetEncryptedChunk(args, !isEncrypted) :
                    await GetBestEncryptedChunkAsync(args, compactLevel, isEncrypted).ConfigureAwait(false);
                
                args.Reference = encryptionResult.Reference;
                args.SpanData = encryptionResult.SpanData;
            }

            await nextStage.FeedAsync(args).ConfigureAwait(false);
        }

        public Task<SwarmReference> SumAsync(SwarmChunkBmt swarmChunkBmt, CancellationToken cancellationToken) =>
            nextStage.SumAsync(swarmChunkBmt, cancellationToken);
        
        // Helpers.
        private async Task<EncryptedChunkResult> GetBestEncryptedChunkAsync(
            HasherPipelineFeedArgs args,
            ushort keysToTest,
            bool useRandomKeys)
        {
            /*
             * Searching for the best encrypted chunk, use optimistic calculation.
             * 
             * Calculate an encryption key, and try to find a bucket with optimal collisions.
             * Before to proceed with the chunk and its key, wait optimistically until
             * the previous chunk has been completed. Then verify if the same bucket has received
             * new collisions, or not. If not, proceed, otherwise try again to search the best chunk key.
             *
             * When not random, the chunk key is calculated from the plain chunk hash, replacing the last 2 bytes
             * with the attempt counter (ushort), and then hashing again.
             * 
             *     chunkKey = Keccack(plainChunkHash[^2..] + attempt)
             *
             * Encrypted chunk is calculated encrypting span and data with the chunk key.
             *
             * The optimistic algorithm will search the first best chunk available, trying a max number of keys.
             *
             * Best chunk is a chunk that fits in a bucket with the lowest possible number of collisions.
             *
             * Use a cache on generated chunkKey and relative bucketId for each attempt. This permits to not
             * repeat the hash calculation in case that we need to repeat the search.
             *
             * In case that a chunk hash has already been stored into stamp store, accept it without optimistic check.
             * This is the best case of all, because it will not increment any bucket. Anyway, still wait
             * prev chunk completion because we want to avoid this otherwise possible case:
             * - sequential input chunks are [0, 1, 2, ...]
             * - chunk 1 is the only one with its hash previously stamped
             * - hashing of 0 keeps long time to complete,
             * - hashing of 1 immediately proceeds because was already stamped, and
             * - from 2 onwards can proceed with optimization without the need of wait for 0 to be inserted
             */
            if (keysToTest == 0)
                throw new ArgumentOutOfRangeException(nameof(keysToTest), "Keys to test can't be zero");
            
            // If not using random keys, hash chunk and clear chunkBmt for next uses.
            byte[]? plainChunkHashArray = null;
            if (!useRandomKeys)
            {
                var plainChunkHash = args.SwarmChunkBmt.Hash(args.SpanData);
                args.SwarmChunkBmt.Clear();
                plainChunkHashArray = plainChunkHash.ToByteArray();
            }
                
            // Run optimistically before prev chunk completion.
            var encryptionCache = new Dictionary<ushort /*attempt*/, EncryptedChunkResult>();
            var (bestKeyAttempt, expectedCollisions, wasAlreadyStamped) =
                SearchFirstBestChunkKey(args, keysToTest, encryptionCache, plainChunkHashArray);

            // If there isn't any prev chunk to wait, proceed with result.
            if (args.PrevChunkSemaphore == null)
                return encryptionCache[bestKeyAttempt];

            try
            {
                // Otherwise wait until prev chunk has been processed.
                await args.PrevChunkSemaphore.WaitAsync().ConfigureAwait(false);
                
                // ** Here chunks can enter only once per time, and in order. **
                
                // If chunk was already stamped, keep it.
                if (wasAlreadyStamped)
                    return encryptionCache[bestKeyAttempt];
            
                // Check the optimistic result, and keep if valid.
                var bestBucketId = encryptionCache[bestKeyAttempt].Reference.Hash.ToBucketId();
                var actualCollisions = PostageStamper.StampIssuer.Buckets.GetCollisions(bestBucketId);
            
                if (actualCollisions == expectedCollisions)
                    return encryptionCache[bestKeyAttempt];

                // If it has been invalidated, do it again.
                _missedOptimisticHashing++;
                var (newBestKeyAttempt, _, _) = SearchFirstBestChunkKey(args, keysToTest, encryptionCache, plainChunkHashArray);
                return encryptionCache[newBestKeyAttempt];
            }
            finally
            {
                //release prev chunk semaphore to reuse it
                args.PrevChunkSemaphore.Release();
            }
        }

        private static EncryptedChunkResult GetEncryptedChunk(
            HasherPipelineFeedArgs args,
            bool deterministic)
        {
            // If not using random keys, generate deterministic key.
            EncryptionKey256? chunkKey = null;
            SwarmHash? plainChunkHash = null;
            if (deterministic)
            {
                plainChunkHash = args.SwarmChunkBmt.Hash(args.SpanData);
                args.SwarmChunkBmt.Clear();
                chunkKey = args.SwarmChunkBmt.Hasher.ComputeHash(plainChunkHash.Value.ToReadOnlyMemory().Span);
            }
                
            // Encrypt chunk and extract key.
            chunkKey = ChunkEncrypter.EncryptChunk(
                args.SpanData.Span[..SwarmCac.SpanSize],
                args.SpanData.Span[SwarmCac.SpanSize..],
                chunkKey,
                args.SwarmChunkBmt.Hasher,
                out var encryptedSpanData,
                deterministic ? plainChunkHash!.Value.ToReadOnlyMemory() : (ReadOnlyMemory<byte>?)null);
            
            // Calculate the hash and return result.
            var encryptedChunkHash = args.SwarmChunkBmt.Hash(encryptedSpanData);
            args.SwarmChunkBmt.Clear();
            return new EncryptedChunkResult
            {
                Reference = new SwarmReference(encryptedChunkHash, chunkKey.Value),
                SpanData = encryptedSpanData
            };
        }
        
        private (ushort bestKeyAttempt, uint expectedCollisions, bool wasAlreadyStamped) SearchFirstBestChunkKey(
            HasherPipelineFeedArgs args,
            ushort keysToTest,
            Dictionary<ushort /*attempt*/, EncryptedChunkResult> optimisticCache,
            byte[]? plainChunkHashArray)
        {
            // Init.
            ushort bestAttempt = 0;
            uint bestCollisions = 0;
                
            // Search best chunk key.
            for (ushort i = 0; i < keysToTest; i++)
            {
                uint collisions;
                
                if (optimisticCache.TryGetValue(i, out var cachedValues))
                {
                    collisions = PostageStamper.StampIssuer.Buckets.GetCollisions(cachedValues.Reference.Hash.ToBucketId());
                }
                else
                {
                    // Generate deterministic key (if not random).
                    EncryptionKey256? chunkKey = null;
                    if (plainChunkHashArray != null)
                    {
                        BinaryPrimitives.WriteUInt16BigEndian(plainChunkHashArray.AsSpan()[^2..], i);
                        chunkKey = new EncryptionKey256(args.SwarmChunkBmt.Hasher.ComputeHash(plainChunkHashArray));
                    }
                    
                    // Encrypt chunk and extract key.
                    chunkKey = ChunkEncrypter.EncryptChunk(
                        args.SpanData.Span[..SwarmCac.SpanSize],
                        args.SpanData.Span[SwarmCac.SpanSize..],
                        chunkKey,
                        args.SwarmChunkBmt.Hasher,
                        out var encryptedSpanData,
                        plainChunkHashArray?.AsMemory());
                    
                    // Calculate hash, bucket id, and save in cache.
                    var encryptedHash = args.SwarmChunkBmt.Hash(encryptedSpanData);
                    args.SwarmChunkBmt.Clear();
                    optimisticCache[i] = new EncryptedChunkResult
                    {
                        Reference = new SwarmReference(encryptedHash, chunkKey.Value),
                        SpanData = encryptedSpanData
                    };

                    // Check key collisions.
                    collisions = PostageStamper.StampIssuer.Buckets.GetCollisions(encryptedHash.ToBucketId());
                }
                
                // First attempt is always the best one.
                if (i == 0)
                    bestCollisions = collisions;
                
                // Check if hash was already stamped.
                if (PostageStamper.StampStore.TryGet(
                        StampStoreItem.BuildId(PostageStamper.StampIssuer.PostageBatch.Id, optimisticCache[i].Reference.Hash),
                        out _))
                    return (i, collisions, true);
                
                // Check if collisions are optimal.
                if (collisions == PostageStamper.StampIssuer.Buckets.MinBucketCollisions)
                    return (i, collisions, false);
                
                // Else, if this reaches better collisions, but not the best.
                if (collisions < bestCollisions)
                {
                    bestAttempt = i;
                    bestCollisions = collisions;
                }
            }

            return (bestAttempt, bestCollisions, false);
        }
    }
}