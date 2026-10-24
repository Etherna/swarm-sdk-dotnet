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

namespace Etherna.SwarmSdk.Models
{
    /// <summary>
    /// A postage batch as answered by the node, with the reason the node gives when it could not read it live
    /// </summary>
    public sealed class PostageBatchResponse(
        PostageBatch postageBatch,
        string? error)
    {
        /// <summary>
        /// The reason the batch could not be read live: Beehive answers a batch whose owner node cannot answer with
        /// only the values it knows. Null when the batch is live, and always with Bee
        /// </summary>
        /// <remarks>
        /// When set, <see cref="PostageBatch.BlockNumber"/>, <see cref="PostageBatch.Exists"/> and
        /// <see cref="PostageBatch.Ttl"/> are unknown and read as their defaults
        /// </remarks>
        public string? Error { get; } = error;

        public PostageBatch PostageBatch { get; } = postageBatch;
    }
}
