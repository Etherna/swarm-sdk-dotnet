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
        public Task RequestReceived => requestReceived.Task;
        public List<HttpRequestMessage> Requests { get; } = [];

        // Methods.
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
