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

using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk
{
    /// <summary>
    /// An HTTP handler answering every request with one status code, body and set of headers, recording the
    /// requests and their bodies
    /// </summary>
    public sealed class StatusAnsweringHttpMessageHandler(
        HttpStatusCode statusCode,
        string content = "",
        IReadOnlyDictionary<string, string>? responseHeaders = null,
        bool declaresContentLength = true)
        : HttpMessageHandler
    {
        // Properties.
        /// <summary>
        /// A task the handler waits for before it reads the body of a request, as a transport sending the body once
        /// its connection is ready; null to read it at once
        /// </summary>
        public Task? BodyReadingGate { get; set; }
        public List<byte[]?> RequestBodies { get; } = [];
        public List<HttpRequestMessage> Requests { get; } = [];
        
        // Methods.
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (BodyReadingGate is not null)
                await BodyReadingGate;
            RequestBodies.Add(request.Content is null
                ? null
                : await request.Content.ReadAsByteArrayAsync(cancellationToken));
            
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
                RequestMessage = request
            };
            
            //a header describing the content is refused among the ones of the response
            foreach (var (name, value) in responseHeaders ?? new Dictionary<string, string>())
                if (!response.Headers.TryAddWithoutValidation(name, value))
                    response.Content.Headers.TryAddWithoutValidation(name, value);

            //a content lists its length among its headers only when it is declared, as a node does
            if (declaresContentLength)
                response.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(content);
            return response;
        }
    }
}
