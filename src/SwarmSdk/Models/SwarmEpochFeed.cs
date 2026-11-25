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
using Etherna.SwarmSdk.Stores;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Models
{
    public sealed class SwarmEpochFeed(EthAddress owner, SwarmFeedTopic topic)
        : SwarmFeedBase(owner, topic)
    {
        // Properties.
        public override SwarmFeedType Type => SwarmFeedType.Epoch;

        // Methods.
        public override async Task<SwarmFeedChunkBase> BuildNextFeedChunkAsync(
            ReadOnlyMemory<byte> data,
            SwarmFeedIndexBase? knownNearIndex,
            IReadOnlyChunkStore chunkStore,
            SwarmChunkBmt chunkBmt,
            DateTimeOffset? timestamp = null,
            CancellationToken cancellationToken = default)
        {
            if (knownNearIndex is not (null or SwarmEpochFeedIndex))
                throw new ArgumentException("Feed index bust be null or epoch index", nameof(knownNearIndex));
            
            return await BuildNextFeedChunkAsync(
                data,
                knownNearIndex as SwarmEpochFeedIndex,
                chunkStore,
                chunkBmt,
                timestamp,
                cancellationToken).ConfigureAwait(false);
        }

        public override SwarmFeedChunkBase SocToFeedChunk(SwarmSoc soc, SwarmFeedIndexBase index) =>
            SwarmEpochFeedChunk.BuildFromSoc(soc, index, Topic);

        public async Task<SwarmEpochFeedChunk> BuildNextFeedChunkAsync(
            ReadOnlyMemory<byte> data,
            SwarmEpochFeedIndex? knownNearIndex,
            IReadOnlyChunkStore chunkStore,
            SwarmChunkBmt swarmChunkBmt,
            DateTimeOffset? timestamp = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(swarmChunkBmt);
            
            var at = DateTimeOffset.UtcNow;

            // Find last published chunk.
            var lastFeedChunk = await TryFindFeedAtAsync(
                at, knownNearIndex, chunkStore, swarmChunkBmt.Hasher, cancellationToken).ConfigureAwait(false);

            // Define next epoch index.
            SwarmEpochFeedIndex nextEpochIndex;
            if (lastFeedChunk is null)
            {
                nextEpochIndex = new SwarmEpochFeedIndex(0, SwarmEpochFeedIndex.MaxLevel, swarmChunkBmt.Hasher);
                if (!nextEpochIndex.ContainsTime(at))
                    nextEpochIndex = nextEpochIndex.Right;
            }
            else
                nextEpochIndex = (SwarmEpochFeedIndex)lastFeedChunk.Index.GetNext(at);

            // Create new chunk.
            return new SwarmEpochFeedChunk(
                Topic,
                nextEpochIndex,
                BuildIdentifier(nextEpochIndex, swarmChunkBmt.Hasher),
                Owner,
                SwarmEpochFeedChunk.BuildInnerChunk(data, timestamp, swarmChunkBmt),
                null);
        }
        
        public override async Task<SwarmFeedChunkBase?> TryFindFeedChunkAtAsync(
            long at,
            SwarmFeedIndexBase? knownNearIndex,
            IReadOnlyChunkStore chunkStore,
            Hasher hasher,
            CancellationToken cancellationToken = default)
        {
            if (knownNearIndex is not (null or SwarmEpochFeedIndex))
                throw new ArgumentException("Feed index bust be null or epoch index", nameof(knownNearIndex));
            
            return await TryFindFeedAtAsync(
                DateTimeOffset.FromUnixTimeSeconds(at),
                knownNearIndex as SwarmEpochFeedIndex,
                chunkStore,
                hasher,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<SwarmEpochFeedChunk?> TryFindFeedAtAsync(
            DateTimeOffset at,
            SwarmEpochFeedIndex? knownNearEpochIndex,
            IReadOnlyChunkStore chunkStore,
            Hasher hasher,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(hasher);
            
            if (at < DateTimeOffset.FromUnixTimeSeconds((long)SwarmEpochFeedIndex.MinUnixTimeStamp) ||
                at > DateTimeOffset.FromUnixTimeSeconds((long)SwarmEpochFeedIndex.MaxUnixTimeStamp))
                throw new ArgumentOutOfRangeException(nameof(at), "Date is out of allowed range");

            var atUnixTime = (ulong)at.ToUnixTimeSeconds();

            /*
             * This look up is composed by different phases:
             * 
             * Phase 1) Find a starting epoch index to look up containing the date. (bottom->up)
             * 
             * This doesn't access to swarm network, so it ignores chunks' timestamps.
             * It starts from an optional well known existing epoch index passed by user, and tries to find index of an existing starting chunk.
             * Passed epoch index should be as near as possible to the final chunk to maximize optimization.
             * Process is tolerant to wrong initial passed index, even if in this case it is not optimized.
             * 
             * It tries to find common ancestor between date and existing previously known epoch, if passed.
             * If a previously known epoch is not passed, start at max level with epoch containing the date.
             * 
             * -> Input: optional known existing epoch index, near to searched date.
             * <- Output: starting epoch index that could not exist as a chunk, or chunk timestamp could be subsequent to searched date,
             *    but epoch contains the date.
             * 
             * ------------
             * Phase 2) Find a starting chunk prior to the searched date. (bottom->up)
             * 
             * Verify if selected chunk on phase 1 exists, and if its timestamp is prior to the searched date.
             * If it doesn't exist, or if time stamp is subsequent to searched date,
             *   if epoch index is right, try to search on left,
             *   else if epoch index is left, try to search on parent.
             * Stops when a chunk with previous date is found, or when it reaches max level limit on left chunk.
             * 
             * -> Input: starting epoch index from phase 1.
             * <- Output: an existing chunk with prior date, or null if a chunk is not found. If null, skip phase 3.
             * 
             * ------------
             * Phase 3) Find the existing chunk with timestamp nearest and prior to the searched date. (top->down)
             * 
             * It starts from the output chunk of phase 2, and tries to get near as possible to searched date, without pass it.
             * Is possible that, if the passed chunk is a left chunk, epoch index of passed chunk could not contain the "at" date.
             * In this case adjust the date as (chunk.Index.Right.Start - 1).
             * 
             * It tries to get child epoch at date from existing chunk. If chunk exists and is prior, make recursion on it.
             * If it doesn't exist, or it has timestamp subsequent to date.
             *   If child is right, try to get left. Check again end eventually make recursion on it.
             *   If child is left return current chunk.
             * It stops when a valid chunk to continue recursion is not found, or when current chunk hit level 0 (max resolution).
             * 
             * -> Input: starting chunk from phase 2.
             * <- Output: the chunk with nearest timestamp prior to searched date.
             */

            // Phase 1)
            var startEpoch = FindStartingEpochOffline(knownNearEpochIndex, atUnixTime, hasher);

            // Phase 2)
            var startChunk = await TryFindStartingEpochChunkOnlineAsync(
                chunkStore,
                atUnixTime,
                startEpoch,
                hasher,
                cancellationToken).ConfigureAwait(false);

            if (startChunk is null)
                return null;

            // Phase 3)
            return await FindLastEpochChunkBeforeDateAsync(
                chunkStore,
                atUnixTime,
                startChunk,
                hasher,
                cancellationToken).ConfigureAwait(false);
        }

        // Helpers.
        internal async Task<SwarmEpochFeedChunk> FindLastEpochChunkBeforeDateAsync(
            IReadOnlyChunkStore chunkStore,
            ulong at,
            SwarmEpochFeedChunk currentChunk,
            Hasher hasher,
            CancellationToken cancellationToken)
        {
            // If currentChunk is at max resolution, return it.
            var currentIndex = (SwarmEpochFeedIndex)currentChunk.Index;
            if (currentIndex.Level == SwarmEpochFeedIndex.MinLevel)
                return currentChunk;

            // Normalize "at" date. Possible if we are trying a left epoch, but date is contained at right.
            if (!currentIndex.ContainsTime(at))
                at = currentIndex.Right.Start - 1;

            // Try chunk on child epoch at date.
            var childIndexAtDate = currentIndex.GetChildAt(at);
            var childChunkAtDate = await TryGetFeedChunkAsync(
                childIndexAtDate, chunkStore, hasher, cancellationToken).ConfigureAwait(false);
            if (childChunkAtDate is SwarmEpochFeedChunk epochChildChunkAtDate
                && (ulong)epochChildChunkAtDate.TimeStamp.ToUnixTimeSeconds() <= at)
                return await FindLastEpochChunkBeforeDateAsync(
                    chunkStore, at, epochChildChunkAtDate, hasher, cancellationToken).ConfigureAwait(false);

            // Try left brother if different.
            if (childIndexAtDate.IsRight)
            {
                var childLeftChunk = await TryGetFeedChunkAsync(
                    childIndexAtDate.Left, chunkStore, hasher, cancellationToken).ConfigureAwait(false);
                if (childLeftChunk is SwarmEpochFeedChunk epochChildLeftChunk) //to check timestamp is superfluous in this case
                    return await FindLastEpochChunkBeforeDateAsync(
                        chunkStore, at, epochChildLeftChunk, hasher, cancellationToken).ConfigureAwait(false);
            }

            return currentChunk;
        }

        /// <summary>
        /// Implement phase 1 of epoch chunk look up.
        /// </summary>
        /// <param name="knownNearEpoch">An optional epoch index with known existing chunk</param>
        /// <param name="at">The searched date</param>
        /// <param name="hasher"></param>
        /// <returns>A starting epoch index</returns>
        internal static SwarmEpochFeedIndex FindStartingEpochOffline(
            SwarmEpochFeedIndex? knownNearEpoch,
            ulong at,
            Hasher hasher)
        {
            var startEpoch = knownNearEpoch;
            if (startEpoch is not null)
            {
                //traverse parents until find a common ancestor
                while (startEpoch.Level != SwarmEpochFeedIndex.MaxLevel &&
                    !startEpoch.ContainsTime(at))
                    startEpoch = startEpoch.Parent;

                //if max level is reached and start epoch still doesn't contain the time, drop it
                if (!startEpoch.ContainsTime(at))
                    startEpoch = null;
            }

            //if start epoch is null (known near was null or max epoch level is hit)
            startEpoch ??= new SwarmEpochFeedIndex(0, SwarmEpochFeedIndex.MaxLevel, hasher);
            if (!startEpoch.ContainsTime(at))
                startEpoch = startEpoch.Right;
            return startEpoch;
        }

        /// <summary>
        /// Implement phase 2 of epoch chunk look up.
        /// </summary>
        /// <param name="chunkStore">The chunk store</param>
        /// <param name="at">The searched date</param>
        /// <param name="epochIndex">The epoch to analyze containing current date</param>
        /// <param name="hasher">The hasher</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A tuple with found chunk (if any) and updated "at" date</returns>
        internal async Task<SwarmEpochFeedChunk?> TryFindStartingEpochChunkOnlineAsync(
            IReadOnlyChunkStore chunkStore,
            ulong at,
            SwarmEpochFeedIndex epochIndex,
            Hasher hasher,
            CancellationToken cancellationToken)
        {
            // Try to get chunk payload on network.
            var chunk = await TryGetFeedChunkAsync(
                epochIndex, chunkStore, hasher, cancellationToken).ConfigureAwait(false);

            // If chunk exists and date is prior.
            if (chunk is SwarmEpochFeedChunk epochFeedChunk &&
                (ulong)epochFeedChunk.TimeStamp.ToUnixTimeSeconds() <= at)
                return epochFeedChunk;

            // Else, if chunk is not found, or if chunk timestamp is later than target date.
            if (epochIndex.IsRight)                               //try left
                return await TryFindStartingEpochChunkOnlineAsync(
                    chunkStore, at, epochIndex.Left, hasher, cancellationToken).ConfigureAwait(false);
            if (epochIndex.Level != SwarmEpochFeedIndex.MaxLevel) //try parent
                return await TryFindStartingEpochChunkOnlineAsync(
                    chunkStore, at, epochIndex.Parent, hasher, cancellationToken).ConfigureAwait(false);

            return null;
        }
    }
}