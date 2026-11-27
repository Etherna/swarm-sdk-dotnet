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
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Wasm
{
    /// <summary>
    /// The upload tags of a bee node, exported to JavaScript as <c>Etherna.SwarmSdk.Wasm.TagExports</c>. A tag
    /// counts the chunks of an upload as the node splits, stores and syncs them; its id is a number. Results
    /// are the JSON of the tags (<see cref="WasmJsonContext"/>).
    /// </summary>
    internal static partial class TagExports
    {
        // Methods.
        /// <summary>
        /// Create a tag, optionally for the hex address of a content, and return its JSON.
        /// </summary>
        [JSExport]
        internal static async Task<string> CreateTagAsync(string beeUrl, string? address)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return JsonSerializer.Serialize(
                await swarmClient.CreateTagAsync(address is null ? SwarmHash.Zero : new SwarmHash(address))
                    .ConfigureAwait(false),
                WasmJsonContext.Default.TagInfo);
        }

        /// <summary>
        /// Delete a tag.
        /// </summary>
        [JSExport]
        internal static async Task DeleteTagAsync(string beeUrl, [JSMarshalAs<JSType.Number>] long tagId)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            await swarmClient.DeleteTagAsync(new TagId((ulong)tagId)).ConfigureAwait(false);
        }

        /// <summary>
        /// The JSON of a tag.
        /// </summary>
        [JSExport]
        internal static async Task<string> GetTagAsync(string beeUrl, [JSMarshalAs<JSType.Number>] long tagId)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return JsonSerializer.Serialize(
                await swarmClient.GetTagInfoAsync(new TagId((ulong)tagId)).ConfigureAwait(false),
                WasmJsonContext.Default.TagInfo);
        }

        /// <summary>
        /// The JSON of a page of the tags of the node.
        /// </summary>
        [JSExport]
        internal static async Task<string> ListTagsAsync(string beeUrl, int? offset, int? limit)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return JsonSerializer.Serialize(
                await swarmClient.GetTagsListAsync(offset, limit).ConfigureAwait(false),
                WasmJsonContext.Default.TagInfoArray);
        }

        /// <summary>
        /// Close a tag, optionally setting the hex address of its content.
        /// </summary>
        [JSExport]
        internal static async Task UpdateTagAsync(
            string beeUrl,
            [JSMarshalAs<JSType.Number>] long tagId,
            string? address)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            await swarmClient.UpdateTagAsync(
                new TagId((ulong)tagId),
                address is null ? (SwarmHash?)null : new SwarmHash(address)).ConfigureAwait(false);
        }
    }
}
