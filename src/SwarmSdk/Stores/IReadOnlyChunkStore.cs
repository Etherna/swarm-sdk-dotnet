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
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Stores
{
    public interface IReadOnlyChunkStore
    {
        /// <summary>
        /// Get a chunk
        /// </summary>
        /// <param name="hash">The chunk's hash</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The chunk</returns>
        /// <exception cref="KeyNotFoundException">The chunk is not found</exception>
        Task<SwarmChunk> GetAsync(
            SwarmHash hash,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Try to get multiple chunks
        /// </summary>
        /// <param name="hashes">Chunks' hashes to find</param>
        /// <param name="canReturnAfterFailed">If set, the function can return after has failed this number of chunks</param>
        /// <param name="canReturnAfterSucceeded">If set, the function can return after has found this number of chunks</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A dictionary listing searched chunks by their hashes. A value is null when the chunk is not found</returns>
        Task<IReadOnlyDictionary<SwarmHash, SwarmChunk?>> GetAsync(
            IEnumerable<SwarmHash> hashes,
            int? canReturnAfterFailed = null,
            int? canReturnAfterSucceeded = null,
            CancellationToken cancellationToken = default);
        
        Task<bool> HasChunkAsync(SwarmHash hash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Try to get a chunk
        /// </summary>
        /// <param name="hash">The chunk's hash</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The chunk, or null when the chunk is not found. Any other read failure propagates</returns>
        Task<SwarmChunk?> TryGetAsync(
            SwarmHash hash,
            CancellationToken cancellationToken = default);
    }
}