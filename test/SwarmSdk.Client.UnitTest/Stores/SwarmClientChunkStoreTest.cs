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
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Stores
{
    public class SwarmClientChunkStoreTest
    {
        // Data.
        /// <summary>
        /// The failures the node client can answer with, none of them meaning that the chunk is not found:
        /// an api error other than 404, a request timeout.
        /// </summary>
        public static IEnumerable<object[]> ReadFailures =>
        [
            [new SwarmSdkApiException("Internal Server Error", 500, "read chunk failed", new Dictionary<string, IEnumerable<string>>(), null)],
            [new SwarmSdkApiException("Service Unavailable", 503, "", new Dictionary<string, IEnumerable<string>>(), null)],
            [new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout", new TimeoutException())]
        ];
        
        // Fields.
        private static readonly SwarmHash chunkHash = "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9";
        
        // Tests.
        [Theory, MemberData(nameof(ReadFailures))]
        public async Task GetPropagatesReadFailure(Exception readFailure)
        {
            // Setup.
            var chunkStore = new SwarmClientChunkStore(BuildFailingSwarmClient(readFailure));
            
            // Run & Assert.
            var thrown = await Assert.ThrowsAnyAsync<Exception>(() => chunkStore.GetAsync(chunkHash));
            Assert.Same(readFailure, thrown);
        }
        
        [Fact]
        public async Task GetReturnsTheClientChunk()
        {
            // Setup.
            var chunk = new SwarmCac(chunkHash, new byte[SwarmCac.SpanSize + 42]);
            var swarmClientMock = new Mock<ISwarmClient>();
            swarmClientMock.Setup(c => c.GetChunkAsync(
                    chunkHash,
                    It.IsAny<SwarmChunkBmt>(),
                    It.IsAny<bool?>(),
                    It.IsAny<long?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(chunk);
            var chunkStore = new SwarmClientChunkStore(swarmClientMock.Object);
            
            // Run.
            var result = await chunkStore.GetAsync(chunkHash);
            
            // Assert.
            Assert.Same(chunk, result);
        }
        
        [Fact]
        public async Task GetThrowsKeyNotFoundOnNotFoundAnswer()
        {
            // Setup.
            var notFound = BuildNotFoundAnswer();
            var chunkStore = new SwarmClientChunkStore(BuildFailingSwarmClient(notFound));
            
            // Run.
            var thrown = await Assert.ThrowsAsync<KeyNotFoundException>(() => chunkStore.GetAsync(chunkHash));
            
            // Assert.
            Assert.Same(notFound, thrown.InnerException);
        }
        
        [Fact]
        public async Task TryGetAnswersNullOnNotFoundAnswer()
        {
            // Setup.
            var chunkStore = new SwarmClientChunkStore(BuildFailingSwarmClient(BuildNotFoundAnswer()));
            
            // Run.
            var chunk = await chunkStore.TryGetAsync(chunkHash);
            
            // Assert.
            Assert.Null(chunk);
        }
        
        [Theory, MemberData(nameof(ReadFailures))]
        public async Task TryGetPropagatesReadFailure(Exception readFailure)
        {
            // Setup.
            var chunkStore = new SwarmClientChunkStore(BuildFailingSwarmClient(readFailure));
            
            // Run & Assert.
            var thrown = await Assert.ThrowsAnyAsync<Exception>(() => chunkStore.TryGetAsync(chunkHash));
            Assert.Same(readFailure, thrown);
        }
        
        // Helpers.
        private static ISwarmClient BuildFailingSwarmClient(Exception readFailure)
        {
            var swarmClientMock = new Mock<ISwarmClient>();
            swarmClientMock.Setup(c => c.GetChunkAsync(
                    It.IsAny<SwarmHash>(),
                    It.IsAny<SwarmChunkBmt>(),
                    It.IsAny<bool?>(),
                    It.IsAny<long?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(readFailure);
            return swarmClientMock.Object;
        }
        
        private static SwarmSdkApiException BuildNotFoundAnswer() =>
            new("Not Found", 404, "chunk not found", new Dictionary<string, IEnumerable<string>>(), null);
    }
}
