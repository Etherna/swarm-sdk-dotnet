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
using Etherna.SwarmSdk.Wasm.Json;
using Etherna.SwarmSdk.Wasm.Nodes;
using System;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Wasm
{
    /// <summary>
    /// Postage batches, exported to JavaScript as <c>Etherna.SwarmSdk.Wasm.PostageExports</c>: the batches of a
    /// bee node and of the chain, their purchase, top up, dilution and label, and the offline arithmetic of
    /// their price, duration and depth.
    /// </summary>
    /// <remarks>
    /// Amounts of BZZ are strings of PLUR, amounts of xDAI strings of wei, durations seconds. Structured results
    /// are JSON (<see cref="WasmJsonContext"/>).
    /// </remarks>
    internal static partial class PostageExports
    {
        // Methods.
        /// <summary>
        /// Buy a postage batch with the wallet of the node, and return the JSON of its id and of the transaction.
        /// </summary>
        [JSExport]
        internal static async Task<string> BuyPostageBatchAsync(
            string beeUrl,
            string amount,
            int depth,
            string? label,
            bool? immutable,
            [JSMarshalAs<JSType.Number>] long? gasLimit,
            string? gasPrice)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var (batchId, txHash) = await swarmClient.BuyPostageBatchAsync(
                BzzValue.FromPlurString(amount),
                depth,
                label,
                immutable,
                (ulong?)gasLimit,
                BeeNode.ReadXDai(gasPrice)).ConfigureAwait(false);
            return JsonSerializer.Serialize(
                new PostageBatchPurchase(batchId.ToString(), txHash.ToString()),
                WasmJsonContext.Default.PostageBatchPurchase);
        }

        /// <summary>
        /// The amount per chunk that buys the given duration at the given chain price per chunk per block.
        /// </summary>
        [JSExport]
        internal static string CalculateAmount(
            string chainPrice,
            [JSMarshalAs<JSType.Number>] long ttlSeconds) =>
            PostageBatch.CalculateAmount(BzzValue.FromPlurString(chainPrice), TimeSpan.FromSeconds(ttlSeconds))
                .ToPlurString();

        /// <summary>
        /// The price of a batch of the given depth with the given amount per chunk.
        /// </summary>
        [JSExport]
        internal static string CalculatePrice(string amount, int depth) =>
            PostageBatch.CalculatePrice(BzzValue.FromPlurString(amount), depth).ToPlurString();

        /// <summary>
        /// The duration in seconds the given amount per chunk buys at the given chain price.
        /// </summary>
        [JSExport]
        [return: JSMarshalAs<JSType.Number>]
        internal static long CalculateTtl(string amount, string chainPrice) =>
            (long)PostageBatch.CalculateTtl(BzzValue.FromPlurString(amount), BzzValue.FromPlurString(chainPrice))
                .TotalSeconds;

        /// <summary>
        /// Dilute a postage batch to a greater depth, and return the hash of the transaction.
        /// </summary>
        [JSExport]
        internal static async Task<string> DilutePostageBatchAsync(
            string beeUrl,
            string batchId,
            int depth,
            string? gasPrice,
            [JSMarshalAs<JSType.Number>] long? gasLimit)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return (await swarmClient.DilutePostageBatchAsync(
                new PostageBatchId(batchId),
                depth,
                BeeNode.ReadXDai(gasPrice),
                (ulong?)gasLimit).ConfigureAwait(false)).ToString();
        }

        /// <summary>
        /// The JSON of the valid postage batches of the chain, with their owners, or of the one with the given id.
        /// </summary>
        [JSExport]
        internal static async Task<string> GetGlobalValidPostageBatchesAsync(string beeUrl, string? batchId)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var batches = await swarmClient.GetGlobalValidPostageBatchesAsync(
                batchId is null ? (PostageBatchId?)null : new PostageBatchId(batchId)).ConfigureAwait(false);
            return JsonSerializer.Serialize(
                batches.Select(b => new GlobalPostageBatch(b.PostageBatch, b.Owner)).ToArray(),
                WasmJsonContext.Default.GlobalPostageBatchArray);
        }

        /// <summary>
        /// The JSON of the postage batches the node owns.
        /// </summary>
        [JSExport]
        internal static async Task<string> GetOwnedPostageBatchesAsync(string beeUrl)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return JsonSerializer.Serialize(
                await swarmClient.GetOwnedPostageBatchesAsync().ConfigureAwait(false),
                WasmJsonContext.Default.PostageBatchResponseArray);
        }

        /// <summary>
        /// The JSON of a postage batch of the node.
        /// </summary>
        [JSExport]
        internal static async Task<string> GetPostageBatchAsync(string beeUrl, string batchId)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return JsonSerializer.Serialize(
                await swarmClient.GetPostageBatchAsync(new PostageBatchId(batchId)).ConfigureAwait(false),
                WasmJsonContext.Default.PostageBatchResponse);
        }

        /// <summary>
        /// The JSON of the bucket collisions the node counts for a postage batch.
        /// </summary>
        [JSExport]
        internal static async Task<string> GetPostageBatchBucketsAsync(string beeUrl, string batchId)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return JsonSerializer.Serialize(
                await swarmClient.GetPostageBatchBucketsAsync(new PostageBatchId(batchId)).ConfigureAwait(false),
                WasmJsonContext.Default.PostageBucketsStatus);
        }

        /// <summary>
        /// The chunks a bucket of a batch of the given depth holds.
        /// </summary>
        [JSExport]
        [return: JSMarshalAs<JSType.Number>]
        internal static long GetMaxBucketCollisions(int depth) =>
            PostageBuckets.PostageBatchDepthToMaxCollisions(depth);

        /// <summary>
        /// The depth a batch needs for its fullest bucket to hold the given chunks.
        /// </summary>
        [JSExport]
        internal static int GetRequiredBatchDepth([JSMarshalAs<JSType.Number>] long collisions) =>
            PostageBuckets.CollisionsToRequiredPostageBatchDepth(checked((uint)collisions));

        /// <summary>
        /// Top up a postage batch with an amount per chunk, and return the hash of the transaction.
        /// </summary>
        [JSExport]
        internal static async Task<string> TopUpPostageBatchAsync(
            string beeUrl,
            string batchId,
            string amount,
            string? gasPrice,
            [JSMarshalAs<JSType.Number>] long? gasLimit)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return (await swarmClient.TopUpPostageBatchAsync(
                new PostageBatchId(batchId),
                BzzValue.FromPlurString(amount),
                BeeNode.ReadXDai(gasPrice),
                (ulong?)gasLimit).ConfigureAwait(false)).ToString();
        }

        /// <summary>
        /// Change the label of a postage batch of the node.
        /// </summary>
        [JSExport]
        internal static async Task UpdatePostageBatchLabelAsync(string beeUrl, string batchId, string label)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            await swarmClient.UpdatePostageBatchLabelAsync(new PostageBatchId(batchId), label).ConfigureAwait(false);
        }
    }
}
