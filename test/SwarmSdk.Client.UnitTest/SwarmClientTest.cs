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
        // Consts.
        private const string DegradedBatchJson =
            """{"batchID":"aa48dd9f8c1a62ecbca14bbe76ee35f2bb1fd5c89bd5e3e32c0d0e1d4cdf1b6e","utilization":3,"usable":false,"label":"","depth":20,"amount":null,"bucketDepth":16,"immutableFlag":false,"error":"Node is not ready"}""";
        private const string FullBatchJson =
            """{"batchID":"42d923558a0fcec12cd6c84ef4118f32caa694d60ae677e1ebe76ad8e470d099","utilization":0,"usable":true,"label":"","depth":17,"amount":"2831120640","bucketDepth":16,"blockNumber":44667643,"immutableFlag":false,"exists":true,"batchTTL":120105}""";
        
        // Fields.
        private static readonly SwarmHash chunkHash = "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9";
        private static readonly Uri nodeUrl = new("http://localhost:1633/");
        
        // Tests.
        [Fact]
        public async Task GetOwnedPostageBatchesInBeehiveModeReadsTheErrorOfADegradedEntry()
        {
            // Setup.
            var handler = new StatusAnsweringHttpMessageHandler(
                HttpStatusCode.OK,
                $$"""{"stamps":[{{FullBatchJson}},{{DegradedBatchJson}}]}""");
            using var swarmClient = new SwarmClient(nodeUrl, SwarmClients.Beehive, new HttpClient(handler));
            
            // Run.
            var responses = await swarmClient.GetOwnedPostageBatchesAsync();
            
            // Assert.
            Assert.Equal(2, responses.Length);
            Assert.Null(responses[0].Error);
            Assert.True(responses[0].PostageBatch.Exists);
            Assert.Equal("Node is not ready", responses[1].Error);
            Assert.False(responses[1].PostageBatch.IsUsable);
            Assert.Equal(20, responses[1].PostageBatch.Depth);
            var request = Assert.Single(handler.Requests);
            Assert.Equal("/stamps", request.RequestUri!.AbsolutePath);
        }
        
        [Fact]
        public async Task GetPostageBatchInBeehiveModeReadsNoErrorFromAFullAnswer()
        {
            // Setup.
            var handler = new StatusAnsweringHttpMessageHandler(HttpStatusCode.OK, FullBatchJson);
            using var swarmClient = new SwarmClient(nodeUrl, SwarmClients.Beehive, new HttpClient(handler));
            
            // Run.
            var response = await swarmClient.GetPostageBatchAsync(
                "42d923558a0fcec12cd6c84ef4118f32caa694d60ae677e1ebe76ad8e470d099");
            
            // Assert.
            Assert.Null(response.Error);
            Assert.True(response.PostageBatch.Exists);
            Assert.Equal(44667643UL, response.PostageBatch.BlockNumber);
            Assert.Equal(TimeSpan.FromSeconds(120105), response.PostageBatch.Ttl);
        }
        
        [Fact]
        public async Task GetPostageBatchInBeehiveModeReadsTheErrorOfADegradedAnswer()
        {
            // Setup.
            var handler = new StatusAnsweringHttpMessageHandler(HttpStatusCode.OK, DegradedBatchJson);
            using var swarmClient = new SwarmClient(nodeUrl, SwarmClients.Beehive, new HttpClient(handler));
            
            // Run.
            var response = await swarmClient.GetPostageBatchAsync(
                "aa48dd9f8c1a62ecbca14bbe76ee35f2bb1fd5c89bd5e3e32c0d0e1d4cdf1b6e");
            
            // Assert.
            Assert.Equal("Node is not ready", response.Error);
            Assert.False(response.PostageBatch.IsUsable);
            Assert.Null(response.PostageBatch.Amount);
            Assert.Equal(20, response.PostageBatch.Depth);
            var request = Assert.Single(handler.Requests);
            Assert.Equal(
                "/stamps/aa48dd9f8c1a62ecbca14bbe76ee35f2bb1fd5c89bd5e3e32c0d0e1d4cdf1b6e",
                request.RequestUri!.AbsolutePath);
        }
        
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
