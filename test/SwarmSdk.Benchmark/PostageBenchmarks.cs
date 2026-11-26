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
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;

namespace Etherna.SwarmSdk.Benchmark
{
    /// <summary>
    /// The postage buckets of an issuer, and the fake stamper the services and the tests build
    /// </summary>
    [MemoryDiagnoser]
    public class PostageBenchmarks
    {
        // Benchmarks.
        [Benchmark]
        public FakePostageStamper BuildFakePostageStamper() => new();

        [Benchmark]
        public void BuildPostageBuckets()
        {
            using var buckets = new PostageBuckets();
        }
    }
}
