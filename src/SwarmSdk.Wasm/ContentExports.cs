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
using Etherna.SwarmSdk.Services;
using Etherna.SwarmSdk.Stores;
using Etherna.SwarmSdk.Wasm.Contents;
using Etherna.SwarmSdk.Wasm.Json;
using Etherna.SwarmSdk.Wasm.Postage;
using Etherna.SwarmSdk.Wasm.Stores;
using System;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Wasm
{
    /// <summary>
    /// Chunking and hashing of contents, exported to JavaScript as <c>Etherna.SwarmSdk.Wasm.ContentExports</c>.
    /// Nothing leaves the process: the references are computed locally, and the chunks are handed to the
    /// optional <c>onChunk</c> callback, with their hex address, full payload and postage stamp, for the caller
    /// to upload.
    /// </summary>
    /// <remarks>
    /// A redundancy level is the number of <see cref="Models.RedundancyLevel"/>: 0 none, 1 medium, 2 strong,
    /// 3 insane, 4 paranoid. A compaction level is the number of keys tried per chunk to fill the postage
    /// buckets evenly, 0 for none: a compacted chunk is encrypted, and referenced with its key. A stamper is a
    /// number from <see cref="StamperExports.CreateStamper"/>: with one the chunks are stamped, and compacted
    /// against the buckets of its batch; without, the stamps passed to <c>onChunk</c> are null. A null chunk
    /// concurrency takes the default of the SDK. Results are the JSON of a <see cref="ContentResult"/>.
    /// </remarks>
    internal static partial class ContentExports
    {
        // Methods.
        /// <summary>
        /// Count the chunks an upload of a content of the given length writes: data, intermediate and parity
        /// chunks, and the replicas of the root, assuming no chunk repeats another.
        /// </summary>
        [JSExport]
        [return: JSMarshalAs<JSType.Number>]
        internal static long CountContentChunks(
            [JSMarshalAs<JSType.Number>] long dataLength,
            int redundancyLevel,
            bool encrypt,
            int compactLevel)
        {
            var options = ContentOptions.Parse(redundancyLevel, encrypt, compactLevel);
            return new ChunkService().CountContentChunks(
                dataLength,
                options.RedundancyLevel,
                options.Encrypt,
                options.CompactLevel);
        }

        /// <summary>
        /// The hex address of a content addressed chunk from its full payload, span and data: the BMT hash a bee
        /// node verifies it with.
        /// </summary>
        [JSExport]
        internal static string GetChunkAddress(byte[] payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            if (payload.Length is < SwarmCac.SpanSize or > SwarmCac.SpanDataSize)
                throw new ArgumentOutOfRangeException(
                    nameof(payload),
                    $"A chunk payload is {SwarmCac.SpanSize} to {SwarmCac.SpanDataSize} bytes");
            return new SwarmChunkBmt().Hash(payload).ToString();
        }

        /// <summary>
        /// Chunk raw bytes, as a bee node does on its bytes endpoint, and return the JSON of their reference and of the postage they take.
        /// </summary>
        [JSExport]
        internal static async Task<string> HashBytesAsync(
            byte[] data,
            int redundancyLevel,
            bool encrypt,
            int compactLevel,
            int? stamper,
            int? chunkConcurrency,
            [JSMarshalAs<JSType.Function<JSType.String, JSType.Any, JSType.Any>>]
            Action<string, object, object?>? onChunk)
        {
            var options = ContentOptions.Parse(redundancyLevel, encrypt, compactLevel);
            var postageStamper = StamperExports.BuildWriteStamper(stamper);
            var result = await ContentWriter.WriteBytesAsync(
                data,
                options,
                BuildChunkStore(onChunk, postageStamper),
                postageStamper,
                chunkConcurrency).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, WasmJsonContext.Default.ContentResult);
        }

        /// <summary>
        /// Chunk a directory, as a bee node does on its bzz endpoint with a tar collection, and return the
        /// JSON of the reference of its manifest and of the postage it takes. The files are given as their paths,
        /// the concatenation of their contents in the same order, and the length of each content.
        /// </summary>
        [JSExport]
        internal static async Task<string> HashDirectoryAsync(
            string[] filePaths,
            byte[] contents,
            int[] contentLengths,
            string? indexFilename,
            string? errorFilename,
            int redundancyLevel,
            bool encrypt,
            int compactLevel,
            int? stamper,
            int? chunkConcurrency,
            [JSMarshalAs<JSType.Function<JSType.String, JSType.Any, JSType.Any>>]
            Action<string, object, object?>? onChunk)
        {
            var options = ContentOptions.Parse(redundancyLevel, encrypt, compactLevel);
            var postageStamper = StamperExports.BuildWriteStamper(stamper);
            var result = await ContentWriter.WriteDirectoryAsync(
                filePaths,
                contents,
                contentLengths,
                indexFilename,
                errorFilename,
                options,
                BuildChunkStore(onChunk, postageStamper),
                postageStamper,
                chunkConcurrency).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, WasmJsonContext.Default.ContentResult);
        }

        /// <summary>
        /// Chunk a single file, as a bee node does on its bzz endpoint, and return the JSON of the reference of
        /// its manifest and of the postage it takes.
        /// </summary>
        [JSExport]
        internal static async Task<string> HashFileAsync(
            byte[] data,
            string contentType,
            string? fileName,
            int redundancyLevel,
            bool encrypt,
            int compactLevel,
            int? stamper,
            int? chunkConcurrency,
            [JSMarshalAs<JSType.Function<JSType.String, JSType.Any, JSType.Any>>]
            Action<string, object, object?>? onChunk)
        {
            var options = ContentOptions.Parse(redundancyLevel, encrypt, compactLevel);
            var postageStamper = StamperExports.BuildWriteStamper(stamper);
            var result = await ContentWriter.WriteFileAsync(
                data,
                contentType,
                fileName,
                options,
                BuildChunkStore(onChunk, postageStamper),
                postageStamper,
                chunkConcurrency).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, WasmJsonContext.Default.ContentResult);
        }

        // Helpers.
        private static IChunkStore BuildChunkStore(
            Action<string, object, object?>? onChunk,
            RecordingPostageStamper postageStamper) =>
            onChunk is null ? new FakeChunkStore() : new CallbackChunkStore(onChunk, postageStamper);
    }
}
