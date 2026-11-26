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
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Services;
using System;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Benchmark
{
    /// <summary>
    /// A single file of 100 MB uploaded through <see cref="ChunkService"/>, with a stamper with real buckets. A run
    /// lasts seconds: it is measured on a few runs, as a monitoring job does
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 1, iterationCount: 5)]
    public class UploadLargeBenchmarks
    {
        // Fields.
        private readonly ChunkService chunkService = new();
        private byte[] content = [];

        // Parameters.
        [Params((ushort)0, (ushort)16)]
        public ushort CompactLevel { get; set; }

        // Setup.
        [GlobalSetup]
        public void Setup()
        {
            content = new byte[100_000_000];
            new Random(0).NextBytes(content);
        }

        // Benchmarks.
        [Benchmark]
        public async Task<UploadEvaluationResult> UploadOneHundredMegabytes() =>
            await chunkService.UploadSingleFileAsync(
                content,
                "application/octet-stream",
                "content.bin",
                new Hasher(),
                compactLevel: CompactLevel);
    }
}
