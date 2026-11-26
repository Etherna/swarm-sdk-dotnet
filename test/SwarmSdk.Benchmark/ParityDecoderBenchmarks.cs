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

using BenchmarkDotNet.Attributes;
using Etherna.SwarmSdk.Chunks;
using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Benchmark
{
    /// <summary>
    /// The recovery of a full Paranoid level, 39 data and 89 parity chunks, with 20 of its data chunks missing from
    /// the store: the parity decoder fetches the shards there are and decodes the missing ones
    /// </summary>
    [MemoryDiagnoser]
    public class ParityDecoderBenchmarks
    {
        // Fields.
        private readonly MemoryChunkStore chunkStore = new();
        private ReadOnlyMemory<byte> rootSpanData;

        // Setup.
        [GlobalSetup]
        public async Task SetupAsync()
        {
            var content = new byte[SwarmCac.DataSize * 39];
            new Random(0).NextBytes(content);
            using var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStore, new FakePostageStamper(), RedundancyLevel.Paranoid, false, 0, null);
            var reference = await pipeline.HashDataAsync(content);
            rootSpanData = ((SwarmCac)await chunkStore.GetAsync(reference.Hash)).SpanData;

            var decoder = new ChunkParityDecoder(rootSpanData.Span, false, chunkStore);
            foreach (var dataShard in decoder.ShardReferences.Where(s => !s.IsParity).Take(20))
                await chunkStore.RemoveAsync(dataShard.Reference.Hash);
        }

        // Benchmarks.
        [Benchmark]
        public async Task RecoverParanoidLevel()
        {
            var decoder = new ChunkParityDecoder(rootSpanData.Span, false, chunkStore);
            await decoder.FetchAndRecoverAsync(RedundancyStrategy.Race, false);
        }
    }
}
