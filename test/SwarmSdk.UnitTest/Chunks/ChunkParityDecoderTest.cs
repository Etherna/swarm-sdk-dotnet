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

using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Chunks
{
    public class ChunkParityDecoderTest
    {
        // Internal classes.
        public record CanFetchChunkWithStrategyTestElement(
            IChunkStore ChunkStore,
            RedundancyStrategy Strategy,
            bool ExpectedResult);
        
        public record CanGetChunkTestElement(
            SwarmHash ChunkHash,
            Type? ExpectedExceptionType);
        
        // Consts.
        private static readonly SwarmShardReference[] references =
        [
            new("ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac", false),
            new("45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf", false),
            new("6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9", false),
            new("88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac", true),
            new("eda7ecf100a309dd745988090af3cb26e6891118fb1e3ea99986cc5ffbd5dd30", true),
            new("b9c30db9d81835586397913e555569807575998569c67f7ab04fa312d66aafbc", true)
        ];
        private static readonly IReadOnlyDictionary<SwarmHash, SwarmCac> chunksDictionary = new Dictionary<SwarmHash, SwarmCac>()
        {
            ["ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac"] = new( //data
                "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                Convert.FromBase64String("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")),
            
            ["45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf"] = new( //data
                "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                Convert.FromBase64String("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")),
            
            ["6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9"] = new( //data
                "6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9",
                Convert.FromBase64String("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")),
            
            ["88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac"] = new( //parity
                "88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac",
                Convert.FromBase64String("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")),
            
            ["eda7ecf100a309dd745988090af3cb26e6891118fb1e3ea99986cc5ffbd5dd30"] = new( //parity
                "eda7ecf100a309dd745988090af3cb26e6891118fb1e3ea99986cc5ffbd5dd30",
                Convert.FromBase64String("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")),
            
            ["b9c30db9d81835586397913e555569807575998569c67f7ab04fa312d66aafbc"] = new( //parity
                "b9c30db9d81835586397913e555569807575998569c67f7ab04fa312d66aafbc",
                Convert.FromBase64String("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"))
        };
        
        // Data.
        public static IEnumerable<object[]> CanFetchChunkWithStrategyTest
        {
            get
            {
                var tests = new List<CanFetchChunkWithStrategyTestElement>();
                
                // Data strategy fails.
                {
                    var chunkStore = new MemoryChunkStore();
                    chunkStore.AddAsync(chunksDictionary["ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac"]).Wait(); //data
                    chunkStore.AddAsync(chunksDictionary["45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf"]).Wait(); //data
                    // chunkStore.AddAsync(chunksDictionary["6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9"]).Wait(); //data, missing
                    chunkStore.AddAsync(chunksDictionary["88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac"]).Wait(); //parity
                    chunkStore.AddAsync(chunksDictionary["eda7ecf100a309dd745988090af3cb26e6891118fb1e3ea99986cc5ffbd5dd30"]).Wait(); //parity
                    chunkStore.AddAsync(chunksDictionary["b9c30db9d81835586397913e555569807575998569c67f7ab04fa312d66aafbc"]).Wait(); //parity
                    
                    tests.Add(new(chunkStore, RedundancyStrategy.Data, false));
                }
                
                // Data strategy succeeds.
                {
                    var chunkStore = new MemoryChunkStore();
                    chunkStore.AddAsync(chunksDictionary["ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac"]).Wait(); //data
                    chunkStore.AddAsync(chunksDictionary["45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf"]).Wait(); //data
                    chunkStore.AddAsync(chunksDictionary["6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9"]).Wait(); //data
                    
                    tests.Add(new(chunkStore, RedundancyStrategy.Data, true));
                }
                
                // Prox strategy fails.
                //not implemented
                
                // Prox strategy succeeds.
                //not implemented
                
                // Race strategy fails.
                {
                    var chunkStore = new MemoryChunkStore();
                    chunkStore.AddAsync(chunksDictionary["ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac"]).Wait(); //data
                    // chunkStore.AddAsync(chunksDictionary["45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf"]).Wait(); //data, missing
                    // chunkStore.AddAsync(chunksDictionary["6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9"]).Wait(); //data, missing
                    // chunkStore.AddAsync(chunksDictionary["88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac"]).Wait(); //parity, missing
                    // chunkStore.AddAsync(chunksDictionary["eda7ecf100a309dd745988090af3cb26e6891118fb1e3ea99986cc5ffbd5dd30"]).Wait(); //parity, missing
                    chunkStore.AddAsync(chunksDictionary["b9c30db9d81835586397913e555569807575998569c67f7ab04fa312d66aafbc"]).Wait(); //parity
                    
                    tests.Add(new(chunkStore, RedundancyStrategy.Race, false));
                }
                
                // Race strategy succeeds.
                {
                    var chunkStore = new MemoryChunkStore();
                    chunkStore.AddAsync(chunksDictionary["ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac"]).Wait(); //data
                    // chunkStore.AddAsync(chunksDictionary["45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf"]).Wait(); //data, missing
                    // chunkStore.AddAsync(chunksDictionary["6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9"]).Wait(); //data, missing
                    chunkStore.AddAsync(chunksDictionary["88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac"]).Wait(); //parity
                    // chunkStore.AddAsync(chunksDictionary["eda7ecf100a309dd745988090af3cb26e6891118fb1e3ea99986cc5ffbd5dd30"]).Wait(); //parity, missing
                    chunkStore.AddAsync(chunksDictionary["b9c30db9d81835586397913e555569807575998569c67f7ab04fa312d66aafbc"]).Wait(); //parity
                    
                    tests.Add(new(chunkStore, RedundancyStrategy.Race, true));
                }
                
                return tests.Select(t => new object[] { t });
            }
        }
        
        public static IEnumerable<object[]> CanGetChunkTest
        {
            get
            {
                var tests = new List<CanGetChunkTestElement>
                {
                    // Find and return data chunk.
                    new("ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                        null),
                    
                    // Find and return parity chunk.
                    new("88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac",
                        null),
                    
                    // Hash is not on child references.
                    new("1232fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                        typeof(KeyNotFoundException)),
                    
                    // Chunk's data is null in buffer.
                    new("6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9",
                        typeof(KeyNotFoundException))
                };

                return tests.Select(t => new object[] { t });
            }
        }
        
        // Tests.
        [Fact]
        public async Task CanAddChunksToStore()
        {
            var sourceChunkStore = new MemoryChunkStore();
            await sourceChunkStore.AddAsync(chunksDictionary["ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac"]); //data
            await sourceChunkStore.AddAsync(chunksDictionary["45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf"]); //data
            await sourceChunkStore.AddAsync(chunksDictionary["88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac"]); //parity
            
            var destinationChunkStore = new MemoryChunkStore();
            
            var decoder = new ChunkParityDecoder(references, sourceChunkStore);
            await decoder.TryFetchChunksAsync(RedundancyStrategy.Race, false);

            await decoder.AddChunksToStoreAsync(destinationChunkStore,
            [
                "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                "88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac"
            ]);
            
            Assert.Equal(sourceChunkStore.AllChunks.Keys.Order(), destinationChunkStore.AllChunks.Keys.Order());
        }
        
        [Fact]
        public void CanConstruct()
        {
            var decoder = new ChunkParityDecoder(references, new MemoryChunkStore());

            Assert.False(decoder.AreDataChunksReady);
            Assert.False(decoder.IsRecoveryPerformed);
            Assert.Equal(references, decoder.ShardReferences);
        }

        [Theory, MemberData(nameof(CanFetchChunkWithStrategyTest))]
        public async Task CanFetchChunkWithStrategy(CanFetchChunkWithStrategyTestElement test)
        {
            var decoder = new ChunkParityDecoder(references, test.ChunkStore);

            var result = await decoder.TryFetchChunksAsync(test.Strategy, false);

            Assert.Equal(test.ExpectedResult, result);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CanFetchChunkWithStrategyFallback(bool useFallback)
        {
            // Setup.
            var chunkStore = new MemoryChunkStore();
            await chunkStore.AddAsync(chunksDictionary["ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac"]); //data
            await chunkStore.AddAsync(chunksDictionary["45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf"]); //data
            // await chunkStore.AddAsync(chunksDictionary["6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9"]); //data, missing
            await chunkStore.AddAsync(chunksDictionary["88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac"]); //parity
            
            var decoder = new ChunkParityDecoder(references, chunkStore);

            // Act.
            var result = await decoder.TryFetchChunksAsync(RedundancyStrategy.Data, useFallback);

            // Assert.
            Assert.Equal(useFallback, result);
        }
        
        [Theory, MemberData(nameof(CanGetChunkTest))]
        public async Task CanGetChunk(CanGetChunkTestElement test)
        {
            var chunkStore = new MemoryChunkStore();
            await chunkStore.AddAsync(chunksDictionary["ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac"]);
            await chunkStore.AddAsync(chunksDictionary["45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf"]);
            // await chunkStore.AddAsync(chunksDictionary["6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9"]); //data, missing
            await chunkStore.AddAsync(chunksDictionary["88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac"]);
            await chunkStore.AddAsync(chunksDictionary["eda7ecf100a309dd745988090af3cb26e6891118fb1e3ea99986cc5ffbd5dd30"]);
            await chunkStore.AddAsync(chunksDictionary["b9c30db9d81835586397913e555569807575998569c67f7ab04fa312d66aafbc"]);
            
            var decoder = new ChunkParityDecoder(references, chunkStore);
            await decoder.TryFetchChunksAsync(RedundancyStrategy.Race, false);

            if (test.ExpectedExceptionType != null)
                Assert.Throws(test.ExpectedExceptionType, () => decoder.GetChunk(test.ChunkHash));
            else
            {
                var chunk = decoder.GetChunk(test.ChunkHash);
                Assert.Equal(test.ChunkHash, chunk.Hash);
            }
        }

        [Fact]
        public async Task CanGetMissingChunks()
        {
            var chunkStore = new MemoryChunkStore();
            await chunkStore.AddAsync(chunksDictionary["ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac"]); //data
            await chunkStore.AddAsync(chunksDictionary["eda7ecf100a309dd745988090af3cb26e6891118fb1e3ea99986cc5ffbd5dd30"]); //parity
            
            var decoder = new ChunkParityDecoder(references, chunkStore);
            await decoder.TryFetchChunksAsync(RedundancyStrategy.Race, false);

            var missingChunks = decoder.GetMissingShards();

            Assert.Equal(
                [
                    new("45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf", false),
                    new("6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9", false),
                    new("88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac", true),
                    new("b9c30db9d81835586397913e555569807575998569c67f7ab04fa312d66aafbc", true)
                ],
                missingChunks);
        }

        [Fact]
        public async Task CanFetchAndRecoverDataChunks()
        {
            var chunkStore = new MemoryChunkStore();
            await chunkStore.AddAsync(chunksDictionary["ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac"]); //data
            await chunkStore.AddAsync(chunksDictionary["45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf"]); //data
            // await chunkStore.AddAsync(chunksDictionary["6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9"]); //data, missing
            await chunkStore.AddAsync(chunksDictionary["88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac"]); //parity
            
            var decoder = new ChunkParityDecoder(references, chunkStore);
            var result = await decoder.TryFetchAndRecoverAsync(RedundancyStrategy.Race, false);
            var recoveredChunk = decoder.GetChunk("6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9");

            Assert.True(result);
            Assert.True(decoder.AreDataChunksReady);
            Assert.True(decoder.IsRecoveryPerformed);
            Assert.Equal("6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9", recoveredChunk.Hash);
            Assert.Equal(chunksDictionary["6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9"].SpanData,
                ((SwarmCac)recoveredChunk).SpanData);
        }

        [Theory, MemberData(nameof(ReadOnlyChunkStoreBaseTest.ReadFailures), MemberType = typeof(ReadOnlyChunkStoreBaseTest))]
        public async Task TryFetchAndRecoverPropagatesReadFailure(Exception readFailure)
        {
            // Setup.
            var chunkStore = new MemoryChunkStore();
            await chunkStore.AddAsync(chunksDictionary["ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac"]); //data
            await chunkStore.AddAsync(chunksDictionary["45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf"]); //data
            await chunkStore.AddAsync(chunksDictionary["6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9"]); //data
            var failingChunkStore = new ReadFailingChunkStore(
                chunkStore,
                "6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9",
                readFailure);
            
            var decoder = new ChunkParityDecoder(references, failingChunkStore);
            
            // Run & Assert.
            var thrown = await Assert.ThrowsAnyAsync<Exception>(() => decoder.TryFetchAndRecoverAsync(RedundancyStrategy.Data, false));
            Assert.Same(readFailure, thrown);
        }
    }
}
