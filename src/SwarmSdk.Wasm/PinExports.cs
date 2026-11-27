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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Wasm
{
    /// <summary>
    /// The pins of a bee node and the stewardship of contents, exported to JavaScript as
    /// <c>Etherna.SwarmSdk.Wasm.PinExports</c>. References are hex strings; a redundancy level is the number
    /// of <see cref="RedundancyLevel"/>, or null to let the node choose. Structured results are JSON
    /// (<see cref="WasmJsonContext"/>).
    /// </summary>
    internal static partial class PinExports
    {
        // Methods.
        /// <summary>
        /// The JSON of the integrity check of every pin of the node: the chunks of each, missing and invalid.
        /// </summary>
        [JSExport]
        internal static async Task<string> CheckAllPinsAsync(string beeUrl)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            List<CheckPinsResult> results = [];
            await foreach (var result in swarmClient.CheckAllPinsAsync().ConfigureAwait(false))
                results.Add(result);
            return JsonSerializer.Serialize(results.ToArray(), WasmJsonContext.Default.CheckPinsResultArray);
        }

        /// <summary>
        /// The JSON of the integrity check of a pin.
        /// </summary>
        [JSExport]
        internal static async Task<string> CheckPinAsync(string beeUrl, string reference)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return JsonSerializer.Serialize(
                await swarmClient.CheckPinsAsync(new SwarmReference(reference)).ConfigureAwait(false),
                WasmJsonContext.Default.CheckPinsResult);
        }

        /// <summary>
        /// Pin a content on the node, and return false if the node can't find it.
        /// </summary>
        [JSExport]
        internal static async Task<bool> CreatePinAsync(string beeUrl, string reference, int? redundancyLevel)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return await swarmClient.CreatePinAsync(
                new SwarmReference(reference),
                redundancyLevel is null ? null : BeeNode.ReadRedundancyLevel(redundancyLevel.Value))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Unpin a content from the node.
        /// </summary>
        [JSExport]
        internal static async Task DeletePinAsync(string beeUrl, string reference)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            await swarmClient.DeletePinAsync(new SwarmReference(reference)).ConfigureAwait(false);
        }

        /// <summary>
        /// The JSON of the references the node pins.
        /// </summary>
        [JSExport]
        internal static async Task<string> GetAllPinsAsync(string beeUrl)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return JsonSerializer.Serialize(
                (await swarmClient.GetAllPinsAsync().ConfigureAwait(false)).Select(r => r.ToString()).ToArray(),
                WasmJsonContext.Default.StringArray);
        }

        /// <summary>
        /// The JSON of the state of a pin.
        /// </summary>
        [JSExport]
        internal static async Task<string> GetPinStatusAsync(string beeUrl, string reference)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return JsonSerializer.Serialize(
                await swarmClient.GetPinStatusAsync(new SwarmReference(reference)).ConfigureAwait(false),
                WasmJsonContext.Default.PinStatus);
        }

        /// <summary>
        /// Check whether every chunk of a content can be retrieved from the network.
        /// </summary>
        [JSExport]
        internal static async Task<bool> IsContentRetrievableAsync(
            string beeUrl,
            string reference,
            int? redundancyLevel)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return await swarmClient.IsContentRetrievableAsync(
                new SwarmReference(reference),
                redundancyLevel is null ? null : BeeNode.ReadRedundancyLevel(redundancyLevel.Value))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Upload again every chunk of a content the node holds locally, stamping them with a batch of the node.
        /// </summary>
        [JSExport]
        internal static async Task ReuploadContentAsync(
            string beeUrl,
            string reference,
            string batchId,
            int? redundancyLevel)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            await swarmClient.ReuploadContentAsync(
                new SwarmReference(reference),
                new PostageBatchId(batchId),
                redundancyLevel is null ? null : BeeNode.ReadRedundancyLevel(redundancyLevel.Value))
                .ConfigureAwait(false);
        }
    }
}
