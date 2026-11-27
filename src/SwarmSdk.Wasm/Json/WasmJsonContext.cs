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
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Etherna.SwarmSdk.Wasm.Json
{
    /// <summary>
    /// The JSON the exports return for structured results, generated at compile time to stay trimming and AOT
    /// safe. Names are camel case; amounts of BZZ are strings of PLUR and amounts of xDAI strings of wei, as
    /// the converters of the SDK write them, so that no value loses precision in a JavaScript number.
    /// </summary>
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        UseStringEnumConverter = true)]
    [JsonSerializable(typeof(ChainState))]
    [JsonSerializable(typeof(CheckPinsResult))]
    [JsonSerializable(typeof(CheckPinsResult[]))]
    [JsonSerializable(typeof(ContentResult))]
    [JsonSerializable(typeof(FeedInfo))]
    [JsonSerializable(typeof(FeedUpdateResult))]
    [JsonSerializable(typeof(GlobalPostageBatch[]))]
    [JsonSerializable(typeof(Health))]
    [JsonSerializable(typeof(NodeInfo))]
    [JsonSerializable(typeof(PinStatus))]
    [JsonSerializable(typeof(PostageBatchPurchase))]
    [JsonSerializable(typeof(PostageBatchResponse))]
    [JsonSerializable(typeof(PostageBatchResponse[]))]
    [JsonSerializable(typeof(PostageBucketsStatus))]
    [JsonSerializable(typeof(ResolvedResource))]
    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(SwarmNodeAddresses))]
    [JsonSerializable(typeof(TagInfo))]
    [JsonSerializable(typeof(TagInfo[]))]
    internal sealed partial class WasmJsonContext : JsonSerializerContext;

    /// <summary>
    /// The result of chunking a content, with the evaluation of the postage it takes, as
    /// <see cref="Services.UploadEvaluationResult"/> gives it. The buckets are those of the stamper of the
    /// write: with a stamper, every chunk of its batch so far; without, the chunks of this content alone.
    /// </summary>
    /// <param name="Reference">The hex reference of the content.</param>
    /// <param name="MissedOptimisticHashing">The chunks whose compaction had to be computed again, because a
    /// chunk before them changed the buckets.</param>
    /// <param name="RequiredBatchDepth">The depth a batch needs for its fullest bucket.</param>
    /// <param name="RequiredBatchByteSize">The bytes a batch of that depth holds.</param>
    /// <param name="ConsumedSize">The bytes the fullest bucket consumes across every bucket.</param>
    /// <param name="RemainingBatchSize">The bytes left in a batch of the required depth.</param>
    internal sealed record ContentResult(
        string Reference,
        long MissedOptimisticHashing,
        int RequiredBatchDepth,
        long RequiredBatchByteSize,
        long ConsumedSize,
        long RemainingBatchSize)
    {
        // Static builders.
        public static ContentResult Build(
            SwarmReference reference,
            long missedOptimisticHashing,
            IReadOnlyPostageBuckets buckets)
        {
            var requiredBatchByteSize = (1L << buckets.RequiredPostageBatchDepth) * SwarmCac.DataSize;
            var consumedSize = buckets.MaxBucketCollisions * (1L << PostageBatch.BucketDepth) * SwarmCac.DataSize;
            return new ContentResult(
                reference.ToString(),
                missedOptimisticHashing,
                buckets.RequiredPostageBatchDepth,
                requiredBatchByteSize,
                consumedSize,
                requiredBatchByteSize - consumedSize);
        }
    }

    /// <summary>
    /// The feed a feed manifest points to.
    /// </summary>
    internal sealed record FeedInfo(
        string Owner,
        string Topic,
        SwarmFeedType Type);

    /// <summary>
    /// An update of a feed: its index, the address of its single owner chunk, its payload and the data chunk
    /// it wraps. Byte arrays are base64 in JSON.
    /// </summary>
    /// <param name="Index">The sequence index, or the epoch start for an epoch feed, as a decimal string.</param>
    /// <param name="EpochLevel">The epoch level, null for a sequence feed.</param>
    /// <param name="Address">The hex address of the update.</param>
    /// <param name="Payload">The payload of the update, as its single owner chunk carries it. Null on a
    /// published update.</param>
    /// <param name="WrappedChunkAddress">The hex address of the data chunk the update wraps: the root of the
    /// content, to download as bytes. Null on a published update.</param>
    /// <param name="WrappedChunkPayload">The span and data of that chunk. Null on a published update.</param>
    /// <param name="PayloadVersion">How the update wraps its chunk: v1 by reference, legacy, or v2
    /// inline. Null on a published update.</param>
    /// <param name="Timestamp">The unix time in seconds of an epoch feed update, null for a sequence feed and
    /// on a published update.</param>
    internal sealed record FeedUpdateResult(
        string Index,
        int? EpochLevel,
        string Address,
        byte[]? Payload,
        string? WrappedChunkAddress,
        byte[]? WrappedChunkPayload,
        SwarmFeedPayloadVersion? PayloadVersion,
        long? Timestamp);

    /// <summary>
    /// A postage batch the chain knows, with its owner.
    /// </summary>
    internal sealed record GlobalPostageBatch(
        PostageBatch PostageBatch,
        EthAddress Owner);

    /// <summary>
    /// A postage batch just bought.
    /// </summary>
    internal sealed record PostageBatchPurchase(
        string BatchId,
        string TxHash);

    /// <summary>
    /// A resource of a manifest, resolved from an address.
    /// </summary>
    /// <param name="Reference">The hex reference of the content of the resource.</param>
    /// <param name="Metadata">The metadata of the resource, as its content type and its file name.</param>
    /// <param name="IsFromErrorDoc">True if the path resolved to the error document of the manifest.</param>
    internal sealed record ResolvedResource(
        string Reference,
        IReadOnlyDictionary<string, string> Metadata,
        bool IsFromErrorDoc);
}
