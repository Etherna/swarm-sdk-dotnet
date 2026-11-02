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
using Microsoft.Extensions.Time.Testing;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk
{
    public class SwarmClientTest
    {
        // Consts.
        private const string AboveLongAmount = "10000000000000000000";
        private const string DegradedBatchJson =
            """{"batchID":"aa48dd9f8c1a62ecbca14bbe76ee35f2bb1fd5c89bd5e3e32c0d0e1d4cdf1b6e","utilization":3,"usable":false,"label":"","depth":20,"amount":null,"bucketDepth":16,"immutableFlag":false,"error":"Node is not ready"}""";
        private const string FullBatchJson =
            """{"batchID":"42d923558a0fcec12cd6c84ef4118f32caa694d60ae677e1ebe76ad8e470d099","utilization":0,"usable":true,"label":"","depth":17,"amount":"2831120640","bucketDepth":16,"blockNumber":44667643,"immutableFlag":false,"exists":true,"batchTTL":120105}""";
        private const string TransactionHashHex = "0x5a1f0c3b9e7d2a4c6b8e0f1d3c5a7b9e1f3d5c7a9b1e3f5d7c9a1b3e5f7d9c1b";
        
        // Fields.
        private static readonly PostageBatchId batchId = "42d923558a0fcec12cd6c84ef4118f32caa694d60ae677e1ebe76ad8e470d099";
        private static readonly SwarmHash chunkHash = "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9";
        private static readonly Uri nodeUrl = new("http://localhost:1633/");
        
        // Data.
        public static TheoryData<SwarmClients, string, TimeSpan, Func<SwarmClient, Task>> OperationClassBounds => new()
        {
            { SwarmClients.Bee, nameof(SwarmClient.BuyPostageBatchAsync), TimeSpan.FromMinutes(10), c => c.BuyPostageBatchAsync(BzzValue.FromPlurDecimal(1), 20) },
            { SwarmClients.Beehive, nameof(SwarmClient.BuyPostageBatchAsync), TimeSpan.FromMinutes(10), c => c.BuyPostageBatchAsync(BzzValue.FromPlurDecimal(1), 20) },
            { SwarmClients.Bee, nameof(SwarmClient.DepositIntoChequebookAsync), TimeSpan.FromSeconds(60), c => c.DepositIntoChequebookAsync(BzzValue.FromPlurDecimal(1)) },
            { SwarmClients.Bee, nameof(SwarmClient.GetChainStateAsync), TimeSpan.FromSeconds(30), c => c.GetChainStateAsync() },
            { SwarmClients.Beehive, nameof(SwarmClient.GetChainStateAsync), TimeSpan.FromSeconds(30), c => c.GetChainStateAsync() },
            { SwarmClients.Bee, nameof(SwarmClient.GetChunkStreamAsync), TimeSpan.FromMinutes(2), c => c.GetChunkStreamAsync(chunkHash) },
            { SwarmClients.Beehive, nameof(SwarmClient.GetChunkStreamAsync), TimeSpan.FromMinutes(2), c => c.GetChunkStreamAsync(chunkHash) },
            { SwarmClients.Bee, nameof(SwarmClient.GetGlobalValidPostageBatchesAsync), TimeSpan.FromSeconds(60), c => c.GetGlobalValidPostageBatchesAsync() },
            { SwarmClients.Beehive, nameof(SwarmClient.GetGlobalValidPostageBatchesAsync), TimeSpan.FromSeconds(60), c => c.GetGlobalValidPostageBatchesAsync() },
            { SwarmClients.Bee, nameof(SwarmClient.OpenChunkUploadWebSocketConnectionAsync), TimeSpan.FromSeconds(5), c => c.GetChunkUploaderWebSocketAsync(batchId) },
            { SwarmClients.Bee, nameof(SwarmClient.TryConnectToPeerAsync), TimeSpan.FromSeconds(60), c => c.TryConnectToPeerAsync("peer") },
            { SwarmClients.Bee, nameof(SwarmClient.UploadBytesAsync), TimeSpan.FromMinutes(10), c => c.UploadBytesAsync(new MemoryStream([1, 2, 3]), batchId) },
            { SwarmClients.Beehive, nameof(SwarmClient.UploadBytesAsync), TimeSpan.FromMinutes(10), c => c.UploadBytesAsync(new MemoryStream([1, 2, 3]), batchId) },
            { SwarmClients.Bee, nameof(SwarmClient.UploadChunkAsync), TimeSpan.FromMinutes(4), c => c.UploadChunkAsync(new MemoryStream([1, 2, 3]), batchId) },
            { SwarmClients.Beehive, nameof(SwarmClient.UploadChunkAsync), TimeSpan.FromMinutes(4), c => c.UploadChunkAsync(new MemoryStream([1, 2, 3]), batchId) },
        };
        
        // Tests.
        [Fact]
        public void ConstructorBuildsItsHttpClientOnTheLongestBound()
        {
            // Setup.
            var timeouts = new SwarmClientTimeouts { ContentTransfer = TimeSpan.FromMinutes(30) };
            
            // Run.
            using var swarmClient = new SwarmClient(nodeUrl, timeouts: timeouts);
            
            // Assert.
            Assert.Same(timeouts, swarmClient.Timeouts);
        }
        
        [Fact]
        public void ConstructorRejectsAnHttpClientTimeoutShorterThanABound()
        {
            // Setup.
            using var httpClient = new HttpClient(new StatusAnsweringHttpMessageHandler(HttpStatusCode.OK))
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            
            // Run.
            var thrown = Assert.Throws<ArgumentException>(() => new SwarmClient(nodeUrl, httpClient: httpClient));
            
            // Assert.
            Assert.Equal("httpClient", thrown.ParamName);
            Assert.StartsWith("The HttpClient timeout (00:05:00) is shorter than the ContentTransfer bound (00:10:00)", thrown.Message, StringComparison.Ordinal);
        }
        
        [Fact]
        public void ConstructorRejectsAnHttpClientTimeoutUnderAnUnboundedClass()
        {
            // Setup.
            using var httpClient = new HttpClient(new StatusAnsweringHttpMessageHandler(HttpStatusCode.OK))
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
            var timeouts = new SwarmClientTimeouts { Local = Timeout.InfiniteTimeSpan };
            
            // Run.
            var thrown = Assert.Throws<ArgumentException>(() => new SwarmClient(nodeUrl, httpClient: httpClient, timeouts: timeouts));
            
            // Assert.
            Assert.Equal("httpClient", thrown.ParamName);
            Assert.Contains("the Local bound", thrown.Message, StringComparison.Ordinal);
        }
        
        [Fact]
        public async Task DepositIntoChequebookInBeeModeSendsAnAmountAboveLongAsAPlurString()
        {
            // Setup.
            var handler = new StatusAnsweringHttpMessageHandler(
                HttpStatusCode.OK,
                $$"""{"transactionHash":"{{TransactionHashHex}}"}""");
            using var swarmClient = new SwarmClient(nodeUrl, SwarmClients.Bee, new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
            
            // Run.
            var txHash = await swarmClient.DepositIntoChequebookAsync(BzzValue.FromPlurString(AboveLongAmount));
            
            // Assert.
            Assert.Equal(TransactionHashHex, txHash.ToString());
            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/chequebook/deposit", request.RequestUri!.AbsolutePath);
            Assert.Equal($"?amount={AboveLongAmount}", request.RequestUri.Query);
        }
        
        [Fact]
        public async Task GetHealthOnAHangingNodePropagatesTheCallerCancellation()
        {
            // Setup.
            var handler = new HangingHttpMessageHandler();
            using var swarmClient = new SwarmClient(
                nodeUrl,
                SwarmClients.Bee,
                new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan },
                timeProvider: new FakeTimeProvider());
            using var cancellationTokenSource = new CancellationTokenSource();
            
            // Run.
            var task = swarmClient.GetHealthAsync(cancellationTokenSource.Token);
            await handler.RequestReceived;
            await cancellationTokenSource.CancelAsync();
            
            // Assert.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        }
        
        [Fact]
        public async Task GetHealthOnAHangingNodeThrowsTheTimeoutAtACustomLocalBound()
        {
            // Setup.
            var handler = new HangingHttpMessageHandler();
            var timeProvider = new FakeTimeProvider();
            using var swarmClient = new SwarmClient(
                nodeUrl,
                SwarmClients.Bee,
                new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan },
                timeouts: new SwarmClientTimeouts { Local = TimeSpan.FromSeconds(1) },
                timeProvider: timeProvider);
            
            // Run.
            var task = swarmClient.GetHealthAsync();
            await handler.RequestReceived;
            timeProvider.Advance(TimeSpan.FromSeconds(1));
            var thrown = await Assert.ThrowsAsync<SwarmClientTimeoutException>(() => task);
            
            // Assert.
            Assert.Equal(TimeSpan.FromSeconds(1), thrown.Timeout);
        }
        
        [Fact]
        public async Task GetHealthOnAHangingNodeThrowsTheTimeoutAtItsLocalBound()
        {
            // Setup.
            var handler = new HangingHttpMessageHandler();
            var timeProvider = new FakeTimeProvider();
            using var swarmClient = new SwarmClient(
                nodeUrl,
                SwarmClients.Bee,
                new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan },
                timeProvider: timeProvider);
            
            // Run.
            var task = swarmClient.GetHealthAsync();
            await handler.RequestReceived;
            timeProvider.Advance(TimeSpan.FromSeconds(5) - TimeSpan.FromMilliseconds(1));
            var isCompletedBeforeTheBound = task.IsCompleted;
            timeProvider.Advance(TimeSpan.FromMilliseconds(1));
            var thrown = await Assert.ThrowsAsync<SwarmClientTimeoutException>(() => task);
            
            // Assert.
            Assert.False(isCompletedBeforeTheBound);
            Assert.Equal("SwarmClient.GetHealthAsync", thrown.Operation);
            Assert.Equal(TimeSpan.FromSeconds(5), thrown.Timeout);
            Assert.Equal("SwarmClient.GetHealthAsync did not complete within 5 s", thrown.Message);
            Assert.Equal("/health", Assert.Single(handler.Requests).RequestUri!.AbsolutePath);
        }
        
        [Fact]
        public async Task GetOwnedPostageBatchesInBeehiveModeReadsTheErrorOfADegradedEntry()
        {
            // Setup.
            var handler = new StatusAnsweringHttpMessageHandler(
                HttpStatusCode.OK,
                $$"""{"stamps":[{{FullBatchJson}},{{DegradedBatchJson}}]}""");
            using var swarmClient = new SwarmClient(nodeUrl, SwarmClients.Beehive, new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
            
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
            using var swarmClient = new SwarmClient(nodeUrl, SwarmClients.Beehive, new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
            
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
            using var swarmClient = new SwarmClient(nodeUrl, SwarmClients.Beehive, new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
            
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
            using var swarmClient = new SwarmClient(nodeUrl, apiCompatibility, new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
            
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
            using var swarmClient = new SwarmClient(nodeUrl, apiCompatibility, new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
            
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
            using var swarmClient = new SwarmClient(nodeUrl, apiCompatibility, new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
            
            // Run.
            var thrown = await Assert.ThrowsAsync<SwarmSdkApiException>(() => swarmClient.IsChunkExistingAsync(chunkHash));
            
            // Assert.
            Assert.Equal((int)statusCode, thrown.StatusCode);
        }
        
        [Theory]
        [MemberData(nameof(OperationClassBounds))]
        public async Task OperationOnAHangingNodeThrowsTheTimeoutAtItsClassBound(
            SwarmClients apiCompatibility,
            string operationName,
            TimeSpan bound,
            Func<SwarmClient, Task> operation)
        {
            // Setup.
            var handler = new HangingHttpMessageHandler();
            var timeProvider = new FakeTimeProvider();
            using var swarmClient = new SwarmClient(
                nodeUrl,
                apiCompatibility,
                new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan },
                timeProvider: timeProvider);
            
            // Run.
            var task = operation(swarmClient);
            await handler.RequestReceived;
            timeProvider.Advance(bound - TimeSpan.FromMilliseconds(1));
            var isCompletedBeforeTheBound = task.IsCompleted;
            timeProvider.Advance(TimeSpan.FromMilliseconds(1));
            var thrown = await Assert.ThrowsAsync<SwarmClientTimeoutException>(() => task);
            
            // Assert.
            Assert.False(isCompletedBeforeTheBound);
            Assert.Equal($"SwarmClient.{operationName}", thrown.Operation);
            Assert.Equal(bound, thrown.Timeout);
        }
        
        [Fact]
        public async Task TopUpPostageBatchInBeeModeSendsAnAmountAndAGasPriceAboveLongAsIntegerStrings()
        {
            // Setup.
            var handler = new StatusAnsweringHttpMessageHandler(
                HttpStatusCode.Accepted,
                $$"""{"batchID":"{{batchId}}","txHash":"{{TransactionHashHex}}"}""");
            using var swarmClient = new SwarmClient(nodeUrl, SwarmClients.Bee, new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
            
            // Run.
            var txHash = await swarmClient.TopUpPostageBatchAsync(
                batchId,
                BzzValue.FromPlurString(AboveLongAmount),
                gasPrice: XDaiValue.FromWeiString(AboveLongAmount));
            
            // Assert.
            Assert.Equal(TransactionHashHex, txHash.ToString());
            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Patch, request.Method);
            Assert.Equal($"/stamps/topup/{batchId}/{AboveLongAmount}", request.RequestUri!.AbsolutePath);
            Assert.Equal(AboveLongAmount, Assert.Single(request.Headers.GetValues("gas-price")));
        }
        
        [Fact]
        public async Task WithdrawFromChequebookInBeeModeSendsAnAmountAndAGasPriceAboveLongAsIntegerStrings()
        {
            // Setup.
            var handler = new StatusAnsweringHttpMessageHandler(
                HttpStatusCode.OK,
                $$"""{"transactionHash":"{{TransactionHashHex}}"}""");
            using var swarmClient = new SwarmClient(nodeUrl, SwarmClients.Bee, new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
            
            // Run.
            var txHash = await swarmClient.WithdrawFromChequebookAsync(
                BzzValue.FromPlurString(AboveLongAmount),
                gasPrice: XDaiValue.FromWeiString(AboveLongAmount));
            
            // Assert.
            Assert.Equal(TransactionHashHex, txHash.ToString());
            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/chequebook/withdraw", request.RequestUri!.AbsolutePath);
            Assert.Equal($"?amount={AboveLongAmount}", request.RequestUri.Query);
            Assert.Equal(AboveLongAmount, Assert.Single(request.Headers.GetValues("gas-price")));
        }
    }
}
