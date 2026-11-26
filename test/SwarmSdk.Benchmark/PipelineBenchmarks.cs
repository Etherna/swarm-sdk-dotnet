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
using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Benchmark
{
    /// <summary>
    /// A hasher pipeline built for one content, as an upload builds one per file and a manifest one per node
    /// </summary>
    [MemoryDiagnoser]
    public class PipelineBenchmarks
    {
        // Fields.
        private readonly FakePostageStamper postageStamper = new();
        private byte[] oneKilobyte = [];
        private byte[] oneMegabyte = [];

        // Setup.
        [GlobalSetup]
        public void Setup()
        {
            oneKilobyte = new byte[1_000];
            oneMegabyte = new byte[1_000_000];
            new Random(0).NextBytes(oneKilobyte);
            new Random(1).NextBytes(oneMegabyte);
        }

        // Benchmarks.
        [Benchmark]
        public void BuildPipeline()
        {
            using var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                new FakeChunkStore(), postageStamper, RedundancyLevel.None, false, 0, null);
        }

        [Benchmark]
        public async Task<SwarmReference> HashOneKilobyte()
        {
            using var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                new FakeChunkStore(), postageStamper, RedundancyLevel.None, false, 0, null);
            return await pipeline.HashDataAsync(oneKilobyte);
        }

        [Benchmark]
        public async Task<SwarmReference> HashOneKilobyteWithParanoidRedundancy()
        {
            using var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                new FakeChunkStore(), postageStamper, RedundancyLevel.Paranoid, false, 0, null);
            return await pipeline.HashDataAsync(oneKilobyte);
        }

        [Benchmark]
        public async Task<SwarmReference> HashOneMegabyte()
        {
            using var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                new FakeChunkStore(), postageStamper, RedundancyLevel.None, false, 0, null);
            return await pipeline.HashDataAsync(oneMegabyte);
        }

        [Benchmark]
        public async Task<SwarmReference> HashOneMegabyteWithParanoidRedundancy()
        {
            using var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                new FakeChunkStore(), postageStamper, RedundancyLevel.Paranoid, false, 0, null);
            return await pipeline.HashDataAsync(oneMegabyte);
        }
    }
}
