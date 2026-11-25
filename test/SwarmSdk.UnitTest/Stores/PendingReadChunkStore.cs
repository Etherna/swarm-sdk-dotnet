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
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Stores
{
    /// <summary>
    /// A read-only chunk store over an inner one, where the reads chosen by their hash and by their number, from 1,
    /// pend until the token they received is cancelled, and end as cancelled
    /// </summary>
    public sealed class PendingReadChunkStore(
        IReadOnlyChunkStore innerChunkStore,
        Func<SwarmHash, int, bool> isPendingRead)
        : ReadOnlyChunkStoreBase
    {
        // Fields.
        private readonly TaskCompletionSource pendingReadStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int reads;

        // Properties.
        public Task PendingReadStarted => pendingReadStarted.Task;
        public int Reads => Volatile.Read(ref reads);

        // Methods.
        public override async Task<SwarmChunk> GetAsync(SwarmHash hash, CancellationToken cancellationToken = default)
        {
            if (isPendingRead(hash, Interlocked.Increment(ref reads)))
            {
                pendingReadStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return await innerChunkStore.GetAsync(hash, cancellationToken);
        }
    }
}
