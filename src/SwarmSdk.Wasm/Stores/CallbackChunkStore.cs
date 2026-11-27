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
    /// A write only chunk store handing every chunk it receives to a JavaScript callback, with the hex
    /// address of the chunk, its full payload and its postage stamp. The payload is span and data of a content
    /// addressed chunk, identifier, signature, span and data of a single owner chunk: what a bee node accepts on
    /// its chunk endpoint. The stamp is the one a stamper issued to the chunk, in the 113 bytes form of the
    /// <c>swarm-postage-stamp</c> header, or null without a stamper.
    /// </summary>
    /// <param name="onChunk">The callback receiving the address, the payload and the stamp of each chunk, byte
    /// arrays the interop hands to JavaScript as <c>Uint8Array</c>.</param>
    /// <param name="postageStamper">The stamper of the write, keeping the stamps of its chunks.</param>
    internal sealed class CallbackChunkStore(
        Action<string, object, object?> onChunk,
        RecordingPostageStamper postageStamper)
        : IChunkStore
    {
        // Methods.
        public Task<bool> AddAsync(SwarmChunk chunk, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(chunk);
            onChunk(
                chunk.Hash.ToString(),
                chunk.GetFullPayloadToByteArray(),
                postageStamper.GetStamp(chunk.Hash)?.ToByteArray());
            return Task.FromResult(true);
        }

        public Task<SwarmChunk> GetAsync(SwarmHash hash, CancellationToken cancellationToken = default) =>
            throw new KeyNotFoundException("Can't get chunks from a callback chunk store");

        public Task<IReadOnlyDictionary<SwarmHash, SwarmChunk?>> GetAsync(
            IEnumerable<SwarmHash> hashes,
            int? canReturnAfterFailed = null,
            int? canReturnAfterSucceeded = null,
            CancellationToken cancellationToken = default) =>
            throw new KeyNotFoundException("Can't get chunks from a callback chunk store");

        public Task<bool> HasChunkAsync(SwarmHash hash, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> RemoveAsync(SwarmHash hash, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<SwarmChunk?> TryGetAsync(SwarmHash hash, CancellationToken cancellationToken = default) =>
            Task.FromResult<SwarmChunk?>(null);
    }
}
