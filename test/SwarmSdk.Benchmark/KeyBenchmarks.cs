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
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Models;
using System.Collections.Generic;
using System.Linq;

namespace Etherna.SwarmSdk.Benchmark
{
    /// <summary>
    /// The addresses derived from a private key, and the replica headers built on the owner of the replicas
    /// </summary>
    [MemoryDiagnoser]
    public class KeyBenchmarks
    {
        // Fields.
        private readonly Hasher hasher = new();
        private readonly PrivateKeySigner signer = new(SwarmSoc.ReplicasOwnerPrivateKey);

        // Benchmarks.
        [Benchmark]
        public List<SwarmReplicaHeader> GenerateParanoidReplicaHeaders() =>
            ChunkReplicator.GenerateReplicaHeaders(SwarmHash.Zero, RedundancyLevel.Paranoid, hasher).ToList();

        [Benchmark]
        public EthAddress ReadReplicasOwner() => SwarmSoc.ReplicasOwner;

        [Benchmark]
        public EthAddress ReadSignerPublicAddress() => signer.PublicAddress;
    }
}
