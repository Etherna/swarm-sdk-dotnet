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
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Stores
{
    public class ReadOnlyChunkStoreBaseTest
    {
        // Data.
        /// <summary>
        /// The read failures a store can answer with: an api error of the node, an infrastructure error,
        /// a request timeout. None of them means that the chunk is not found.
        /// </summary>
        public static IEnumerable<object[]> ReadFailures =>
        [
            [new SwarmSdkApiException("Internal Server Error", 500, "read chunk failed", new Dictionary<string, IEnumerable<string>>(), null)],
            [new InvalidOperationException("No healthy nodes found")],
            [new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout", new TimeoutException())]
        ];
        
        // Tests.
        [Fact]
        public async Task TryGetAnswersNullOnNotFoundChunk()
        {
            // Setup.
            var chunkStore = new ReadFailingChunkStore(new MemoryChunkStore(), SwarmHash.Zero, new InvalidOperationException());
            
            // Run.
            var chunk = await chunkStore.TryGetAsync("1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9");
            
            // Assert.
            Assert.Null(chunk);
        }
        
        [Theory, MemberData(nameof(ReadFailures))]
        public async Task TryGetPropagatesReadFailure(Exception readFailure)
        {
            // Setup.
            var chunkStore = new ReadFailingChunkStore(new MemoryChunkStore(), SwarmHash.Zero, readFailure);
            
            // Run & Assert.
            var thrown = await Assert.ThrowsAnyAsync<Exception>(() => chunkStore.TryGetAsync(SwarmHash.Zero));
            Assert.Same(readFailure, thrown);
        }
    }
}
