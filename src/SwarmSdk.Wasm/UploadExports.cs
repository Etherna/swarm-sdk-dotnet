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
using Etherna.SwarmSdk.Wasm.Contents;
using Etherna.SwarmSdk.Wasm.Json;
using Etherna.SwarmSdk.Wasm.Nodes;
using Etherna.SwarmSdk.Wasm.Postage;
using Etherna.SwarmSdk.Wasm.Stores;
using System;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Wasm
{
    /// <summary>
    /// Uploads to a bee node, exported to JavaScript as <c>Etherna.SwarmSdk.Wasm.UploadExports</c>. A content
    /// is chunked here, as <see cref="ContentExports"/> does, and its chunks are pushed one by one to the chunk
    /// endpoint of the node: the reference is computed locally, and no endpoint chunks the content again.
    /// </summary>
    /// <remarks>
    /// The postage is either the hex id of a batch of the node, which stamps the chunks, or a stamper from
    /// <see cref="StamperExports.CreateStamper"/>, which stamps them here: exactly one of the two. Redundancy and
    /// compaction levels are read as <see cref="ContentExports"/> reads them. A tag, by its number, counts the
    /// chunks of the upload on the node. A null chunk concurrency keeps 16 chunks in flight: the runtime has one
    /// thread, so they overlap their requests to the node, not their hashing. Content uploads return the JSON of
    /// a <see cref="ContentResult"/>.
    /// </remarks>
    internal static partial class UploadExports
    {
        // Consts.
        private const int DefaultUploadChunkConcurrency = 16;

        // Methods.
        /// <summary>
        /// Upload raw bytes, chunked as a bee node does on its bytes endpoint.
        /// </summary>
        [JSExport]
        internal static async Task<string> UploadBytesAsync(
            string beeUrl,
            byte[] data,
            int redundancyLevel,
            bool encrypt,
            int compactLevel,
            string? batchId,
            int? stamper,
            int? chunkConcurrency,
            [JSMarshalAs<JSType.Number>] long? tagId)
        {
            var options = ContentOptions.Parse(redundancyLevel, encrypt, compactLevel);
            var postageStamper = StamperExports.BuildWriteStamper(stamper);
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var result = await ContentWriter.WriteBytesAsync(
                data,
                options,
                BuildChunkStore(swarmClient, batchId, stamper, tagId, postageStamper),
                postageStamper,
                chunkConcurrency ?? DefaultUploadChunkConcurrency).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, WasmJsonContext.Default.ContentResult);
        }

        /// <summary>
        /// Upload a content addressed chunk from its full payload, span and data, and return its hex address.
        /// </summary>
        [JSExport]
        internal static async Task<string> UploadChunkAsync(
            string beeUrl,
            byte[] payload,
            string? batchId,
            int? stamper,
            [JSMarshalAs<JSType.Number>] long? tagId)
        {
            ArgumentNullException.ThrowIfNull(payload);
            if (payload.Length is < SwarmCac.SpanSize or > SwarmCac.SpanDataSize)
                throw new ArgumentOutOfRangeException(
                    nameof(payload),
                    $"A chunk payload is {SwarmCac.SpanSize} to {SwarmCac.SpanDataSize} bytes");

            var nodeBatchId = ReadPostage(batchId, stamper);
            var chunk = new SwarmCac(new SwarmChunkBmt().Hash(payload), payload);
            var stamp = stamper is null ? (PostageStamp?)null : StamperExports.GetStamper(stamper.Value).Stamp(chunk.Hash);

            using var swarmClient = BeeNode.BuildClient(beeUrl);
            await swarmClient.UploadChunkAsync(chunk, nodeBatchId, ReadTagId(tagId), stamp).ConfigureAwait(false);
            return chunk.Hash.ToString();
        }

        /// <summary>
        /// Upload a directory, chunked as a bee node does on its bzz endpoint with a tar collection. The files
        /// are given as <see cref="ContentExports.HashDirectoryAsync"/> takes them.
        /// </summary>
        [JSExport]
        internal static async Task<string> UploadDirectoryAsync(
            string beeUrl,
            string[] filePaths,
            byte[] contents,
            int[] contentLengths,
            string? indexFilename,
            string? errorFilename,
            int redundancyLevel,
            bool encrypt,
            int compactLevel,
            string? batchId,
            int? stamper,
            int? chunkConcurrency,
            [JSMarshalAs<JSType.Number>] long? tagId)
        {
            var options = ContentOptions.Parse(redundancyLevel, encrypt, compactLevel);
            var postageStamper = StamperExports.BuildWriteStamper(stamper);
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var result = await ContentWriter.WriteDirectoryAsync(
                filePaths,
                contents,
                contentLengths,
                indexFilename,
                errorFilename,
                options,
                BuildChunkStore(swarmClient, batchId, stamper, tagId, postageStamper),
                postageStamper,
                chunkConcurrency ?? DefaultUploadChunkConcurrency).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, WasmJsonContext.Default.ContentResult);
        }

        /// <summary>
        /// Upload a single file, chunked as a bee node does on its bzz endpoint.
        /// </summary>
        [JSExport]
        internal static async Task<string> UploadFileAsync(
            string beeUrl,
            byte[] data,
            string contentType,
            string? fileName,
            int redundancyLevel,
            bool encrypt,
            int compactLevel,
            string? batchId,
            int? stamper,
            int? chunkConcurrency,
            [JSMarshalAs<JSType.Number>] long? tagId)
        {
            var options = ContentOptions.Parse(redundancyLevel, encrypt, compactLevel);
            var postageStamper = StamperExports.BuildWriteStamper(stamper);
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var result = await ContentWriter.WriteFileAsync(
                data,
                contentType,
                fileName,
                options,
                BuildChunkStore(swarmClient, batchId, stamper, tagId, postageStamper),
                postageStamper,
                chunkConcurrency ?? DefaultUploadChunkConcurrency).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, WasmJsonContext.Default.ContentResult);
        }

        /// <summary>
        /// Upload a signed single owner chunk from its full payload, as
        /// <see cref="SocExports.BuildSignedSoc"/> returns it, and return its hex address.
        /// </summary>
        [JSExport]
        internal static async Task<string> UploadSocAsync(
            string beeUrl,
            byte[] payload,
            string? batchId,
            int? stamper,
            [JSMarshalAs<JSType.Number>] long? tagId,
            bool deferredUpload,
            bool? pin)
        {
            var nodeBatchId = ReadPostage(batchId, stamper);
            var soc = SwarmSoc.BuildFromBytes(null, payload, new SwarmChunkBmt());
            var stamp = stamper is null ? (PostageStamp?)null : StamperExports.GetStamper(stamper.Value).Stamp(soc.Hash);

            using var swarmClient = BeeNode.BuildClient(beeUrl);
            await swarmClient.UploadSocAsync(soc, nodeBatchId, stamp, ReadTagId(tagId), deferredUpload, pin)
                .ConfigureAwait(false);
            return soc.Hash.ToString();
        }

        // Internal methods.
        /// <summary>
        /// Check that exactly one postage is given, and return the batch the node stamps with, if it's the one.
        /// </summary>
        internal static PostageBatchId? ReadPostage(string? batchId, int? stamper) =>
            (batchId, stamper) switch
            {
                (not null, null) => new PostageBatchId(batchId),
                (null, not null) => (PostageBatchId?)null,
                _ => throw new ArgumentException("Give either a batch id or a stamper", nameof(batchId))
            };

        // Helpers.
        private static BeeUploadChunkStore BuildChunkStore(
            SwarmClient swarmClient,
            string? batchId,
            int? stamper,
            long? tagId,
            RecordingPostageStamper postageStamper) =>
            new(swarmClient, ReadPostage(batchId, stamper), ReadTagId(tagId), postageStamper);

        private static TagId? ReadTagId(long? tagId) =>
            tagId is null ? null : new TagId((ulong)tagId.Value);
    }
}
