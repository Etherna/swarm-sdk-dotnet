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
using System.IO.Pipelines;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk
{
    /// <summary>
    /// A node that answers a request at once with 200, and writes its body while the client reads it: what the test
    /// writes reaches the client as it is written, and the body ends when the test completes it, or fails after
    /// 10 seconds of real time, so that a read nothing ends fails the test instead of hanging it.
    /// </summary>
    public sealed class StreamingHttpMessageHandler : HttpMessageHandler
    {
        // Fields.
        private readonly Pipe body = new();

        // Properties.
        public List<HttpRequestMessage> Requests { get; } = [];

        // Methods.
        public Task CompleteAsync() => body.Writer.CompleteAsync().AsTask();

        public async Task WriteAsync(string text) =>
            await body.Writer.WriteAsync(Encoding.UTF8.GetBytes(text));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            _ = Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None).ContinueWith(
                _ => body.Writer.Complete(new TimeoutException("The body was not ended within the hang time")),
                TaskScheduler.Default);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(body.Reader.AsStream()),
                RequestMessage = request
            });
        }
    }
}
