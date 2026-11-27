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
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Manifest;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Services;
using Etherna.SwarmSdk.Stores;
using Etherna.SwarmSdk.Wasm.Contents;
using Etherna.SwarmSdk.Wasm.Json;
using Etherna.SwarmSdk.Wasm.Nodes;
using Etherna.SwarmSdk.Wasm.Postage;
using Etherna.SwarmSdk.Wasm.Stores;
using System;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Wasm
{
    /// <summary>
    /// Feeds, exported to JavaScript as <c>Etherna.SwarmSdk.Wasm.FeedExports</c>. Lookups run here over single
    /// chunks of the node, each verified against its address and the signature of the owner: a node can't serve
    /// an update the owner didn't sign. Updates are built and signed here, and uploaded stamped by the node or by
    /// a stamper. Feed manifests, the references that point to a feed, are built and decoded here.
    /// </summary>
    /// <remarks>
    /// Owners, topics and keys are hex strings; a feed type is <c>sequence</c> or <c>epoch</c>. A sequence index
    /// is a number; an epoch is its start, a unix time in seconds, and its level. Updates and decoded feeds are
    /// JSON (<see cref="WasmJsonContext"/>), or null when there is none. A postage is either a batch id or a
    /// stamper, as for <see cref="UploadExports"/>.
    /// </remarks>
    internal static partial class FeedExports
    {
        // Methods.
        /// <summary>
        /// Decode a feed manifest from its hex reference, and return the JSON of the feed it points to, or null if
        /// the manifest doesn't point to a feed.
        /// </summary>
        [JSExport]
        internal static async Task<string?> DecodeFeedManifestAsync(string beeUrl, string reference)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var manifest = await ReferencedMantarayManifest.BuildNewAsync(
                new SwarmReference(reference),
                new SwarmClientChunkStore(swarmClient)).ConfigureAwait(false);
            var feed = await new FeedService().TryDecodeFeedManifestAsync(manifest).ConfigureAwait(false);
            return feed is null
                ? null
                : JsonSerializer.Serialize(
                    new FeedInfo(feed.Owner.ToString(), feed.Topic.ToString(), feed.Type),
                    WasmJsonContext.Default.FeedInfo);
        }

        /// <summary>
        /// Find the update of an epoch feed valid at a unix time in seconds, starting from an optional known epoch
        /// near it, and return its JSON, or null if the feed has none before that time.
        /// </summary>
        [JSExport]
        internal static async Task<string?> FindEpochFeedUpdateAsync(
            string beeUrl,
            string owner,
            string topic,
            [JSMarshalAs<JSType.Number>] long at,
            [JSMarshalAs<JSType.Number>] long? knownNearEpochStart,
            int? knownNearEpochLevel)
        {
            var hasher = new Hasher();
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var chunkStore = new SwarmClientChunkStore(swarmClient);
            var feedChunk = await new SwarmEpochFeed(owner, topic).TryFindFeedAtAsync(
                DateTimeOffset.FromUnixTimeSeconds(at),
                ReadEpochIndex(knownNearEpochStart, knownNearEpochLevel, hasher),
                chunkStore,
                hasher).ConfigureAwait(false);
            return feedChunk is null ? null : await SerializeUpdateAsync(feedChunk, chunkStore).ConfigureAwait(false);
        }

        /// <summary>
        /// Find the last update of a sequence feed, starting from an optional known index near it, and return its
        /// JSON, or null if the feed has none.
        /// </summary>
        [JSExport]
        internal static async Task<string?> FindLastSequenceFeedUpdateAsync(
            string beeUrl,
            string owner,
            string topic,
            [JSMarshalAs<JSType.Number>] long? knownNearIndex)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var chunkStore = new SwarmClientChunkStore(swarmClient);
            var feedChunk = await new SwarmSequenceFeed(owner, topic).TryFindLastFeedChunkAsync(
                knownNearIndex is null ? null : new SwarmSequenceFeedIndex((ulong)knownNearIndex.Value),
                chunkStore).ConfigureAwait(false);
            return feedChunk is null ? null : await SerializeUpdateAsync(feedChunk, chunkStore).ConfigureAwait(false);
        }

        /// <summary>
        /// Build the manifest of a feed, as a bee node does on its feeds endpoint, without uploading it, and
        /// return its hex reference. Its chunks go to the optional <c>onChunk</c> callback, as for
        /// <see cref="ContentExports"/>.
        /// </summary>
        [JSExport]
        internal static async Task<string> HashFeedManifestAsync(
            string owner,
            string topic,
            string type,
            int compactLevel,
            int? stamper,
            [JSMarshalAs<JSType.Function<JSType.String, JSType.Any, JSType.Any>>]
            Action<string, object, object?>? onChunk)
        {
            var options = ContentOptions.Parse(0, false, compactLevel);
            var postageStamper = StamperExports.BuildWriteStamper(stamper);
            var reference = await new FeedService().UploadFeedManifestAsync(
                BuildFeed(owner, topic, type),
                new Hasher(),
                options.CompactLevel,
                postageStamper,
                onChunk is null ? new FakeChunkStore() : new CallbackChunkStore(onChunk, postageStamper))
                .ConfigureAwait(false);
            return reference.ToString();
        }

        /// <summary>
        /// Publish the next update of an epoch feed, at a unix time in seconds or now, signed by the private key
        /// of its owner, and return its JSON. The data is read as for <see cref="UpdateSequenceFeedAsync"/>, up to
        /// 4080 bytes: the update takes 8 for its timestamp.
        /// </summary>
        [JSExport]
        internal static async Task<string> UpdateEpochFeedAsync(
            string beeUrl,
            string topic,
            byte[] data,
            bool dataIsChunk,
            string privateKey,
            [JSMarshalAs<JSType.Number>] long? timestamp,
            [JSMarshalAs<JSType.Number>] long? knownNearEpochStart,
            int? knownNearEpochLevel,
            string? batchId,
            int? stamper,
            [JSMarshalAs<JSType.Number>] long? tagId,
            bool deferredUpload,
            bool? pin)
        {
            var key = new EthPrivateKey(privateKey);
            var feed = new SwarmEpochFeed(key.Address, topic);
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var chunkBmt = new SwarmChunkBmt();
            var feedChunk = await feed.BuildNextFeedChunkAsync(
                BuildWrappedChunkPayload(data, dataIsChunk),
                ReadEpochIndex(knownNearEpochStart, knownNearEpochLevel, chunkBmt.Hasher),
                new SwarmClientChunkStore(swarmClient),
                chunkBmt,
                timestamp is null ? null : DateTimeOffset.FromUnixTimeSeconds(timestamp.Value)).ConfigureAwait(false);
            return await PublishUpdateAsync(
                swarmClient, feedChunk, key, chunkBmt, batchId, stamper, tagId, deferredUpload, pin)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Upload the manifest of a feed, built here, and return its hex reference.
        /// </summary>
        [JSExport]
        internal static async Task<string> UploadFeedManifestAsync(
            string beeUrl,
            string owner,
            string topic,
            string type,
            int compactLevel,
            string? batchId,
            int? stamper)
        {
            var options = ContentOptions.Parse(0, false, compactLevel);
            var postageStamper = StamperExports.BuildWriteStamper(stamper);
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var reference = await new FeedService().UploadFeedManifestAsync(
                BuildFeed(owner, topic, type),
                new Hasher(),
                options.CompactLevel,
                postageStamper,
                new BeeUploadChunkStore(swarmClient, UploadExports.ReadPostage(batchId, stamper), null, postageStamper))
                .ConfigureAwait(false);
            return reference.ToString();
        }

        /// <summary>
        /// Publish the next update of a sequence feed, signed by the private key of its owner, and return its JSON.
        /// The update wraps a data chunk inline: the data, up to 4096 bytes, or, with <c>dataIsChunk</c>, the span
        /// and data of a chunk, as the root chunk of a larger content.
        /// </summary>
        [JSExport]
        internal static async Task<string> UpdateSequenceFeedAsync(
            string beeUrl,
            string topic,
            byte[] data,
            bool dataIsChunk,
            string privateKey,
            [JSMarshalAs<JSType.Number>] long? knownNearIndex,
            string? batchId,
            int? stamper,
            [JSMarshalAs<JSType.Number>] long? tagId,
            bool deferredUpload,
            bool? pin)
        {
            var key = new EthPrivateKey(privateKey);
            var feed = new SwarmSequenceFeed(key.Address, topic);
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var chunkBmt = new SwarmChunkBmt();
            var feedChunk = await feed.BuildNextFeedChunkAsync(
                BuildWrappedChunkPayload(data, dataIsChunk),
                knownNearIndex is null ? null : new SwarmSequenceFeedIndex((ulong)knownNearIndex.Value),
                new SwarmClientChunkStore(swarmClient),
                chunkBmt).ConfigureAwait(false);
            return await PublishUpdateAsync(
                swarmClient, feedChunk, key, chunkBmt, batchId, stamper, tagId, deferredUpload, pin)
                .ConfigureAwait(false);
        }

        // Helpers.
        private static SwarmFeedBase BuildFeed(string owner, string topic, string type) =>
            ReadFeedType(type) switch
            {
                SwarmFeedType.Epoch => new SwarmEpochFeed(owner, topic),
                SwarmFeedType.Sequence => new SwarmSequenceFeed(owner, topic),
                _ => throw new InvalidOperationException()
            };

        /// <summary>
        /// The span and data of the chunk an update wraps: the data as a chunk of its own, or already a chunk.
        /// </summary>
        private static byte[] BuildWrappedChunkPayload(byte[] data, bool dataIsChunk)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (dataIsChunk)
            {
                if (data.Length is < SwarmCac.SpanSize or > SwarmCac.SpanDataSize)
                    throw new ArgumentOutOfRangeException(
                        nameof(data),
                        $"A chunk payload is {SwarmCac.SpanSize} to {SwarmCac.SpanDataSize} bytes");
                return data;
            }

            if (data.Length > SwarmCac.DataSize)
                throw new ArgumentOutOfRangeException(nameof(data), $"Data can't exceed {SwarmCac.DataSize} bytes");
            var spanData = new byte[SwarmCac.SpanSize + data.Length];
            SwarmCac.WriteSpan((ulong)data.Length, spanData);
            data.CopyTo(spanData, SwarmCac.SpanSize);
            return spanData;
        }

        private static (string Index, int? EpochLevel) DescribeIndex(SwarmFeedIndexBase index) =>
            index switch
            {
                SwarmEpochFeedIndex epochIndex => (
                    epochIndex.Start.ToString(CultureInfo.InvariantCulture),
                    epochIndex.Level),
                SwarmSequenceFeedIndex sequenceIndex => (
                    sequenceIndex.Value.ToString(CultureInfo.InvariantCulture),
                    null),
                _ => throw new ArgumentException($"Unknown feed index {index.GetType().Name}", nameof(index))
            };

        private static async Task<string> PublishUpdateAsync(
            SwarmClient swarmClient,
            SwarmFeedChunkBase feedChunk,
            EthPrivateKey key,
            SwarmChunkBmt chunkBmt,
            string? batchId,
            int? stamper,
            long? tagId,
            bool deferredUpload,
            bool? pin)
        {
            var nodeBatchId = UploadExports.ReadPostage(batchId, stamper);
            feedChunk.Sign(new PrivateKeySigner(key), chunkBmt.Hasher);
            var stamp = stamper is null
                ? (PostageStamp?)null
                : StamperExports.GetStamper(stamper.Value).Stamp(feedChunk.Hash);

            await swarmClient.UploadSocAsync(
                feedChunk,
                nodeBatchId,
                stamp,
                tagId is null ? null : new TagId((ulong)tagId.Value),
                deferredUpload,
                pin).ConfigureAwait(false);

            var (index, epochLevel) = DescribeIndex(feedChunk.Index);
            return JsonSerializer.Serialize(
                new FeedUpdateResult(index, epochLevel, feedChunk.Hash.ToString(), null, null, null, null, null),
                WasmJsonContext.Default.FeedUpdateResult);
        }

        private static SwarmEpochFeedIndex? ReadEpochIndex(long? start, int? level, Hasher hasher) =>
            (start, level) switch
            {
                (null, null) => null,
                (not null, not null) => new SwarmEpochFeedIndex((ulong)start.Value, checked((byte)level.Value), hasher),
                _ => throw new ArgumentException("An epoch needs both its start and its level", nameof(level))
            };

        private static SwarmFeedType ReadFeedType(string type) =>
            Enum.TryParse<SwarmFeedType>(type, true, out var feedType) && Enum.IsDefined(feedType)
                ? feedType
                : throw new ArgumentOutOfRangeException(nameof(type), "A feed type is sequence or epoch");

        private static async Task<string> SerializeUpdateAsync(
            SwarmFeedChunkBase feedChunk,
            IReadOnlyChunkStore chunkStore)
        {
            var resolved = await feedChunk.ResolveWrappedChunkAsync(new SwarmChunkBmt(), chunkStore)
                .ConfigureAwait(false);
            var (index, epochLevel) = DescribeIndex(feedChunk.Index);
            return JsonSerializer.Serialize(
                new FeedUpdateResult(
                    index,
                    epochLevel,
                    feedChunk.Hash.ToString(),
                    feedChunk.FeedPayload.ToArray(),
                    resolved.Chunk.Hash.ToString(),
                    resolved.Chunk.GetFullPayloadToByteArray(),
                    resolved.Version,
                    feedChunk is SwarmEpochFeedChunk epochChunk ? epochChunk.TimeStamp.ToUnixTimeSeconds() : null),
                WasmJsonContext.Default.FeedUpdateResult);
        }
    }
}
