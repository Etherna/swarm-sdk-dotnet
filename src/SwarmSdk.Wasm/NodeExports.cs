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

using Etherna.SwarmSdk.Wasm.Json;
using Etherna.SwarmSdk.Wasm.Nodes;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Wasm
{
    /// <summary>
    /// The state of a bee node, exported to JavaScript as <c>Etherna.SwarmSdk.Wasm.NodeExports</c>: what a
    /// client checks before using a node. Results are JSON (<see cref="WasmJsonContext"/>).
    /// </summary>
    internal static partial class NodeExports
    {
        // Methods.
        /// <summary>
        /// The JSON of the chain state the node sees, with the current price per chunk per block.
        /// </summary>
        [JSExport]
        internal static async Task<string> GetChainStateAsync(string beeUrl)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return JsonSerializer.Serialize(
                await swarmClient.GetChainStateAsync().ConfigureAwait(false),
                WasmJsonContext.Default.ChainState);
        }

        /// <summary>
        /// The JSON of the health of the node, with its version and the version of its API.
        /// </summary>
        [JSExport]
        internal static async Task<string> GetHealthAsync(string beeUrl)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return JsonSerializer.Serialize(
                await swarmClient.GetHealthAsync().ConfigureAwait(false),
                WasmJsonContext.Default.Health);
        }

        /// <summary>
        /// The JSON of the addresses of the node: overlay, underlays, Ethereum address and keys.
        /// </summary>
        [JSExport]
        internal static async Task<string> GetNodeAddressesAsync(string beeUrl)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return JsonSerializer.Serialize(
                await swarmClient.GetNodeAddressesAsync().ConfigureAwait(false),
                WasmJsonContext.Default.SwarmNodeAddresses);
        }

        /// <summary>
        /// The JSON of the mode of the node and of what it enables.
        /// </summary>
        [JSExport]
        internal static async Task<string> GetNodeInfoAsync(string beeUrl)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return JsonSerializer.Serialize(
                await swarmClient.GetNodeInfoAsync().ConfigureAwait(false),
                WasmJsonContext.Default.NodeInfo);
        }

        /// <summary>
        /// Check whether the node is ready to serve requests.
        /// </summary>
        [JSExport]
        internal static async Task<bool> GetReadinessAsync(string beeUrl)
        {
            using var swarmClient = BeeNode.BuildClient(beeUrl);
            return await swarmClient.GetReadinessAsync().ConfigureAwait(false);
        }
    }
}
