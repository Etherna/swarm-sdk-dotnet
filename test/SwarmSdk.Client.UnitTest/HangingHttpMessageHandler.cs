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

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk
{
    /// <summary>
    /// A node that accepts every request and does not answer it: the call ends through its cancellation token, or
    /// fails after 10 seconds of real time, so that a call nothing cancels fails the test instead of hanging it.
    /// </summary>
    public sealed class HangingHttpMessageHandler : HttpMessageHandler
    {
        // Fields.
        private readonly TaskCompletionSource requestReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Properties.
        public List<HttpRequestMessage> Requests { get; } = [];

        // Methods.
        /// <summary>
        /// Wait for the node to receive the request of an operation. An operation that ends without sending one
        /// fails the wait with its own exception, or with InvalidOperationException when it ended with no error, and
        /// a request that doesn't arrive within 10 seconds of real time fails it with TimeoutException: a test never
        /// waits for a request nothing sends.
        /// </summary>
        public async Task WaitRequestOfAsync(Task operation)
        {
            await Task.WhenAny(requestReceived.Task, operation).WaitAsync(TimeSpan.FromSeconds(10));
            if (requestReceived.Task.IsCompleted)
                return;

            await operation;
            throw new InvalidOperationException("The operation ended without sending a request");
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            requestReceived.TrySetResult();
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            throw new TimeoutException("The request was not cancelled within the hang time");
        }
    }
}
