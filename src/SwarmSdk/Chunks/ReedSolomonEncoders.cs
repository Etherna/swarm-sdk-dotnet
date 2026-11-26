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

using System.Collections.Concurrent;

namespace Etherna.SwarmSdk.Chunks
{
    /// <summary>
    /// The Reed-Solomon encoders of the parity generator and decoder, one per count of data and parity shards for the
    /// process. An encoder is immutable, its encoding and decoding change no state of it, and building one costs more
    /// than a level of a chunk tree takes to encode
    /// </summary>
    internal static class ReedSolomonEncoders
    {
        // Static fields.
        private static readonly ConcurrentDictionary<(int DataShards, int ParityShards), ReedSolomon.NET.ReedSolomon>
            encoders = new();

        // Static methods.
        /// <summary>
        /// The encoder of the shard counts, coding with <see cref="SimdCodingLoop.Best"/>
        /// </summary>
        public static ReedSolomon.NET.ReedSolomon Get(int dataShards, int parityShards) =>
            encoders.GetOrAdd(
                (dataShards, parityShards),
                shards => new ReedSolomon.NET.ReedSolomon(
                    shards.DataShards,
                    shards.ParityShards,
                    SimdCodingLoop.Best));
    }
}
