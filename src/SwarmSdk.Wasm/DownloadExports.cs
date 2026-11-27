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
using Etherna.SwarmSdk.Manifest;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using Etherna.SwarmSdk.Wasm.Json;
using Etherna.SwarmSdk.Wasm.Nodes;
using System;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Wasm
{
    /// <summary>
    /// Verified downloads, exported to JavaScript as <c>Etherna.SwarmSdk.Wasm.DownloadExports</c>. The node
    /// serves single chunks only: their addresses are verified here, the chunk tree is walked, decrypted and
    /// repaired with its parities here, and manifests are resolved here. A node can't serve a content that
    /// isn't the one referenced.
    /// </summary>
    /// <remarks>
    /// A redundancy level is the number of <see cref="RedundancyLevel"/>; here it is the level the root of the
    /// content was replicated with, to look for its replicas when the root is missing. A redundancy strategy is
    /// the number of <see cref="RedundancyStrategy"/>: 0 none, 1 data, 2 prox, 3 race; with the fallback, a
    /// failing strategy falls back to the next, more aggressive one. Contents and chunks arrive in JavaScript as
    /// <c>Uint8Array</c>: the interop returns no byte array from an asynchronous method, so they go through
    /// <c>object</c>.
    /// </remarks>
    internal static partial class DownloadExports
    {
        // Methods.
        /// <summary>
        /// Download raw bytes from their hex reference, as a bee node serves them on its bytes endpoint.
        /// </summary>
        [JSExport]
        [return: JSMarshalAs<JSType.Promise<JSType.Any>>]
        internal static async Task<object> DownloadBytesAsync(
            string beeUrl,
            string reference,
            int redundancyLevel,
            int redundancyStrategy,
            bool redundancyStrategyFallback)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return await ReadContentAsync(
                new SwarmReference(reference),
                new SwarmClientChunkStore(swarmClient),
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback).ConfigureAwait(false);
        }

        /// <summary>
        /// Download a chunk, verified against its hex address, and return its full payload: span and data of a
        /// content addressed chunk, identifier, signature, span and data of a single owner chunk.
        /// </summary>
        [JSExport]
        [return: JSMarshalAs<JSType.Promise<JSType.Any>>]
        internal static async Task<object> DownloadChunkAsync(string beeUrl, string address)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var chunk = await new SwarmClientChunkStore(swarmClient).GetAsync(new SwarmHash(address))
                .ConfigureAwait(false);
            return chunk.GetFullPayloadToByteArray();
        }

        /// <summary>
        /// Download the content whose root chunk is already at hand, from its span and data: the chunk a feed
        /// update wraps inline, which the node doesn't hold at its address. Only the chunks below the root are
        /// downloaded and verified.
        /// </summary>
        [JSExport]
        [return: JSMarshalAs<JSType.Promise<JSType.Any>>]
        internal static async Task<object> DownloadChunkContentAsync(
            string beeUrl,
            byte[] rootChunkPayload,
            int redundancyStrategy,
            bool redundancyStrategyFallback)
        {
            ArgumentNullException.ThrowIfNull(rootChunkPayload);
            if (rootChunkPayload.Length is < SwarmCac.SpanSize or > SwarmCac.SpanDataSize)
                throw new ArgumentOutOfRangeException(
                    nameof(rootChunkPayload),
                    $"A chunk payload is {SwarmCac.SpanSize} to {SwarmCac.SpanDataSize} bytes");

            //a content of one chunk reads without the network: complete asynchronously anyway, the interop fails
            //to marshal a byte array in an object from a task already completed
            await Task.Yield();

            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var rootChunk = new SwarmCac(new SwarmChunkBmt().Hash(rootChunkPayload), rootChunkPayload);
            var dataStream = ChunkDataStream.BuildNew(
                rootChunk,
                new SwarmClientChunkStore(swarmClient),
                BeeNode.ReadRedundancyStrategy(redundancyStrategy),
                redundancyStrategyFallback);
            return await ReadStreamAsync(dataStream).ConfigureAwait(false);
        }

        /// <summary>
        /// Download the resource at a Swarm address, a hex reference with an optional path, as a bee node serves
        /// it on its bzz endpoint. <see cref="ResolveResourceAsync"/> gives its metadata.
        /// </summary>
        /// <param name="beeUrl">The URL of the node.</param>
        /// <param name="address">The Swarm address, as <c>reference/path</c>.</param>
        /// <param name="browserResolution">True to resolve as a browser does: index and error documents, and
        /// redirects to directories; false to take the path as it is.</param>
        /// <param name="redundancyLevel">The redundancy level of the manifest and of the content.</param>
        /// <param name="redundancyStrategy">The strategy to retrieve parity chunks.</param>
        /// <param name="redundancyStrategyFallback">True to fall back to a more aggressive strategy.</param>
        [JSExport]
        [return: JSMarshalAs<JSType.Promise<JSType.Any>>]
        internal static async Task<object> DownloadFileAsync(
            string beeUrl,
            string address,
            bool browserResolution,
            int redundancyLevel,
            int redundancyStrategy,
            bool redundancyStrategyFallback)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var chunkStore = new SwarmClientChunkStore(swarmClient);
            var resource = await ResolveAsync(
                address,
                chunkStore,
                browserResolution,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback).ConfigureAwait(false);
            return await ReadContentAsync(
                resource.Result.Reference,
                chunkStore,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolve the resource at a Swarm address in its manifest, and return the JSON of its reference and of
        /// its metadata, as its content type and file name. Parameters are those of
        /// <see cref="DownloadFileAsync"/>.
        /// </summary>
        [JSExport]
        internal static async Task<string> ResolveResourceAsync(
            string beeUrl,
            string address,
            bool browserResolution,
            int redundancyLevel,
            int redundancyStrategy,
            bool redundancyStrategyFallback)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            var resource = await ResolveAsync(
                address,
                new SwarmClientChunkStore(swarmClient),
                browserResolution,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback).ConfigureAwait(false);
            return JsonSerializer.Serialize(
                new ResolvedResource(
                    resource.Result.Reference.ToString(),
                    resource.Result.Metadata,
                    resource.IsFromErrorDoc),
                WasmJsonContext.Default.ResolvedResource);
        }

        // Helpers.
        private static async Task<byte[]> ReadContentAsync(
            SwarmReference reference,
            IReadOnlyChunkStore chunkStore,
            int redundancyLevel,
            int redundancyStrategy,
            bool redundancyStrategyFallback)
        {
            var dataStream = await ChunkDataStream.BuildNewAsync(
                reference,
                chunkStore,
                BeeNode.ReadRedundancyLevel(redundancyLevel),
                BeeNode.ReadRedundancyStrategy(redundancyStrategy),
                redundancyStrategyFallback).ConfigureAwait(false);
            return await ReadStreamAsync(dataStream).ConfigureAwait(false);
        }

        private static async Task<byte[]> ReadStreamAsync(ChunkDataStream dataStream)
        {
            await using (dataStream.ConfigureAwait(false))
            {
                using var memoryStream = new MemoryStream();
                await dataStream.CopyToAsync(memoryStream).ConfigureAwait(false);
                return memoryStream.ToArray();
            }
        }

        private static Task<ManifestPathResolutionResult<MantarayResourceInfo>> ResolveAsync(
            string address,
            IReadOnlyChunkStore chunkStore,
            bool browserResolution,
            int redundancyLevel,
            int redundancyStrategy,
            bool redundancyStrategyFallback) =>
            SwarmAddressResolver.ResolveResourceInfoAsync(
                new SwarmAddress(address),
                chunkStore,
                browserResolution ? ManifestPathResolver.BrowserResolver : ManifestPathResolver.IdentityResolver,
                BeeNode.ReadRedundancyLevel(redundancyLevel),
                BeeNode.ReadRedundancyStrategy(redundancyStrategy),
                redundancyStrategyFallback);
    }
}
