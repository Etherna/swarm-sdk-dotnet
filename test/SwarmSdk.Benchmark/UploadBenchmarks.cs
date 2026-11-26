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
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Services;
using System;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Benchmark
{
    /// <summary>
    /// A single file uploaded through <see cref="ChunkService"/>, as an application does: the content and its
    /// manifest hashed, every chunk stamped by a stamper with real buckets, the chunks discarded. Compaction picks the
    /// keys of the chunks by the buckets they land in
    /// </summary>
    [MemoryDiagnoser]
    public class UploadBenchmarks
    {
        // Fields.
        private readonly ChunkService chunkService = new();
        private byte[] content = [];

        // Parameters.
        [Params((ushort)0, (ushort)16)]
        public ushort CompactLevel { get; set; }

        [Params(1_000, 1_000_000)]
        public int ContentLength { get; set; }

        [Params(RedundancyLevel.None, RedundancyLevel.Paranoid)]
        public RedundancyLevel RedundancyLevel { get; set; }

        // Setup.
        [GlobalSetup]
        public void Setup()
        {
            content = new byte[ContentLength];
            new Random(0).NextBytes(content);
        }

        // Benchmarks.
        [Benchmark]
        public async Task<UploadEvaluationResult> UploadSingleFile() =>
            await chunkService.UploadSingleFileAsync(
                content,
                "application/octet-stream",
                "content.bin",
                new Hasher(),
                compactLevel: CompactLevel,
                redundancyLevel: RedundancyLevel);
    }
}
