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
using Etherna.SwarmSdk.Stores;
using Etherna.SwarmSdk.Wasm.Postage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Wasm.Stores
{
    /// <summary>
    /// A write only chunk store uploading every chunk it receives to a bee node: a content addressed chunk
    /// on its chunk endpoint, a single owner chunk, as a replica of a root, on its SOC endpoint. A chunk goes
    /// with the stamp a stamper issued to it, or, without one, with the postage batch for the node to stamp it.
    /// </summary>
    /// <param name="swarmClient">The client of the node.</param>
    /// <param name="batchId">The batch the node stamps the chunks with, null when they come stamped.</param>
    /// <param name="tagId">The tag counting the chunks on the node, if any.</param>
    /// <param name="postageStamper">The stamper of the write, keeping the stamps of its chunks.</param>
    internal sealed class BeeUploadChunkStore(
        ISwarmClient swarmClient,
        PostageBatchId? batchId,
        TagId? tagId,
        RecordingPostageStamper postageStamper)
        : IChunkStore
    {
        // Methods.
        public async Task<bool> AddAsync(SwarmChunk chunk, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(chunk);

            var stamp = postageStamper.GetStamp(chunk.Hash);
            if (batchId is null && stamp is null)
                throw new InvalidOperationException($"Chunk {chunk.Hash} has no stamp");

            switch (chunk)
            {
                case SwarmCac cac:
                    await swarmClient.UploadChunkAsync(
                        cac,
                        batchId,
                        tagId,
                        stamp,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
                case SwarmSoc soc:
                    await swarmClient.UploadSocAsync(
                        soc,
                        batchId,
                        stamp,
                        tagId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new ArgumentException($"Unknown chunk type {chunk.GetType().Name}", nameof(chunk));
            }
            return true;
        }

        public Task<SwarmChunk> GetAsync(SwarmHash hash, CancellationToken cancellationToken = default) =>
            throw new KeyNotFoundException("Can't get chunks from an upload chunk store");

        public Task<IReadOnlyDictionary<SwarmHash, SwarmChunk?>> GetAsync(
            IEnumerable<SwarmHash> hashes,
            int? canReturnAfterFailed = null,
            int? canReturnAfterSucceeded = null,
            CancellationToken cancellationToken = default) =>
            throw new KeyNotFoundException("Can't get chunks from an upload chunk store");

        public Task<bool> HasChunkAsync(SwarmHash hash, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> RemoveAsync(SwarmHash hash, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<SwarmChunk?> TryGetAsync(SwarmHash hash, CancellationToken cancellationToken = default) =>
            Task.FromResult<SwarmChunk?>(null);
    }
}
