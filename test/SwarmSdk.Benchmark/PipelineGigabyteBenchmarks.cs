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
using BenchmarkDotNet.Engines;
using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Benchmark
{
    /// <summary>
    /// A hasher pipeline over a content of one gigabyte. A run lasts seconds: it is measured on a few runs, as a
    /// monitoring job does, not repeated as the short benchmarks are. The redundant run is at Medium: a pipeline with
    /// redundancy hashes on less than two cores, and at Paranoid a gigabyte lasts minutes
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 1, iterationCount: 5)]
    public class PipelineGigabyteBenchmarks
    {
        // Fields.
        private readonly FakePostageStamper postageStamper = new();
        private byte[] oneGigabyte = [];

        // Setup.
        [GlobalSetup]
        public void Setup()
        {
            oneGigabyte = new byte[1_000_000_000];
            new Random(2).NextBytes(oneGigabyte);
        }

        // Benchmarks.
        [Benchmark]
        public async Task<SwarmReference> HashOneGigabyte()
        {
            using var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                new FakeChunkStore(), postageStamper, RedundancyLevel.None, false, 0, null);
            return await pipeline.HashDataAsync(oneGigabyte);
        }

        [Benchmark]
        public async Task<SwarmReference> HashOneGigabyteWithMediumRedundancy()
        {
            using var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                new FakeChunkStore(), postageStamper, RedundancyLevel.Medium, false, 0, null);
            return await pipeline.HashDataAsync(oneGigabyte);
        }
    }
}
