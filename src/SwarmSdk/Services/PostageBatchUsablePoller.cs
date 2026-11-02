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

using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Services
{
    /// <summary>
    /// Polls a postage batch until it becomes usable.
    /// </summary>
    public static class PostageBatchUsablePoller
    {
        // Consts.
        /// <summary>Default interval between checks while waiting for a postage batch to become usable.</summary>
        public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);

        /// <summary>Default maximum time to wait for a postage batch to become usable.</summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(15);

        // Static methods.
        /// <summary>
        /// Repeatedly retrieves a postage batch until it is usable, the timeout elapses, or the operation is cancelled.
        /// </summary>
        /// <remarks>
        /// A postage batch is not immediately usable after being bought: the creation transaction must be confirmed
        /// on chain first. Transient "not found yet" (404) and gateway timeout (504) responses that can occur right
        /// after purchase are tolerated and treated as "not usable yet", and so is a read the node does not answer
        /// within its bound (a <see cref="TimeoutException"/> of the delegate).
        /// </remarks>
        /// <param name="batchId">ID of the postage batch to wait for.</param>
        /// <param name="getPostageBatch">Delegate retrieving the current batch state by ID (e.g. <c>swarmClient.GetPostageBatchAsync</c>).</param>
        /// <param name="timeout">Maximum time to wait. Defaults to <see cref="DefaultTimeout"/> (15 minutes).</param>
        /// <param name="pollInterval">Interval between checks. Defaults to <see cref="DefaultPollInterval"/> (5 seconds).</param>
        /// <param name="timeProvider">Time source driving elapsed-time measurement and delays. Defaults to <see cref="TimeProvider.System"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The postage batch once it is usable.</returns>
        /// <exception cref="TimeoutException">The batch did not become usable within the timeout.</exception>
        public static async Task<PostageBatch> WaitUntilUsableAsync(
            PostageBatchId batchId,
            Func<PostageBatchId, CancellationToken, Task<PostageBatch>> getPostageBatch,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null,
            TimeProvider? timeProvider = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(getPostageBatch);

            var effectiveTimeout = timeout ?? DefaultTimeout;
            var effectivePollInterval = pollInterval ?? DefaultPollInterval;
            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            if (effectiveTimeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout), effectiveTimeout, "Timeout cannot be negative");
            if (effectivePollInterval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(pollInterval), effectivePollInterval, "Poll interval must be positive");

            var startTimestamp = effectiveTimeProvider.GetTimestamp();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // A freshly bought batch may not be retrievable yet, or the node may briefly time out, as a gateway
                // timeout or as a read that misses its bound: tolerate those transient answers and keep polling
                // until the batch becomes usable.
                PostageBatch? batch = null;
                try
                {
                    batch = await getPostageBatch(batchId, cancellationToken).ConfigureAwait(false);
                }
                catch (SwarmSdkApiException e) when (e.StatusCode is 404 or 504)
                { }
                catch (TimeoutException)
                { }

                if (batch is { IsUsable: true })
                    return batch;

                if (effectiveTimeProvider.GetElapsedTime(startTimestamp) >= effectiveTimeout)
                    throw new TimeoutException(
                        $"Postage batch {batchId} did not become usable within {effectiveTimeout}");

                await Task.Delay(effectivePollInterval, effectiveTimeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
