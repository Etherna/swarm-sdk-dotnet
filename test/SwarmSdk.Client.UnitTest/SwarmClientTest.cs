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
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk
{
    public class SwarmClientTest
    {
        // Fields.
        private static readonly SwarmHash chunkHash = "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9";
        private static readonly Uri nodeUrl = new("http://localhost:1633/");
        
        // Tests.
        [Theory]
        [InlineData(SwarmClients.Bee)]
        [InlineData(SwarmClients.Beehive)]
        public async Task IsChunkExistingAnswersFalseOnNotFound(SwarmClients apiCompatibility)
        {
            // Setup.
            var handler = new StatusAnsweringHttpMessageHandler(
                HttpStatusCode.NotFound,
                """{"code":404,"message":"chunk not found"}""");
            using var swarmClient = new SwarmClient(nodeUrl, apiCompatibility, new HttpClient(handler));
            
            // Run.
            var isExisting = await swarmClient.IsChunkExistingAsync(chunkHash);
            
            // Assert.
            Assert.False(isExisting);
        }
        
        [Theory]
        [InlineData(SwarmClients.Bee)]
        [InlineData(SwarmClients.Beehive)]
        public async Task IsChunkExistingAnswersTrueOnOk(SwarmClients apiCompatibility)
        {
            // Setup.
            var handler = new StatusAnsweringHttpMessageHandler(HttpStatusCode.OK);
            using var swarmClient = new SwarmClient(nodeUrl, apiCompatibility, new HttpClient(handler));
            
            // Run.
            var isExisting = await swarmClient.IsChunkExistingAsync(chunkHash);
            
            // Assert.
            Assert.True(isExisting);
            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Head, request.Method);
            Assert.Equal($"/chunks/{chunkHash}", request.RequestUri!.AbsolutePath);
        }
        
        [Theory]
        [InlineData(SwarmClients.Bee, HttpStatusCode.InternalServerError)]
        [InlineData(SwarmClients.Bee, HttpStatusCode.ServiceUnavailable)]
        [InlineData(SwarmClients.Beehive, HttpStatusCode.InternalServerError)]
        [InlineData(SwarmClients.Beehive, HttpStatusCode.ServiceUnavailable)]
        public async Task IsChunkExistingPropagatesNodeError(SwarmClients apiCompatibility, HttpStatusCode statusCode)
        {
            // Setup.
            var handler = new StatusAnsweringHttpMessageHandler(statusCode, "read chunk failed");
            using var swarmClient = new SwarmClient(nodeUrl, apiCompatibility, new HttpClient(handler));
            
            // Run.
            var thrown = await Assert.ThrowsAsync<SwarmSdkApiException>(() => swarmClient.IsChunkExistingAsync(chunkHash));
            
            // Assert.
            Assert.Equal((int)statusCode, thrown.StatusCode);
        }
    }
}
