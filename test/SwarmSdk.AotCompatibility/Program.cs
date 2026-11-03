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

namespace Etherna.SwarmSdk.AotCompatibility
{
    using Etherna.SwarmSdk.Hashing;
    using Etherna.SwarmSdk.Models;
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    // Native AOT compatibility probe for SwarmSdk and SwarmSdk.Client. The actual compatibility gate
    // is the ILC publish (see the .csproj and CI): publishing this app fails on any trim/AOT warning.
    // This entry point additionally proves, at runtime, that the two most AOT-sensitive paths work
    // once compiled ahead-of-time: BouncyCastle crypto reached through concrete primitives (Keccak +
    // secp256k1) and System.Text.Json deserialization resolved via the source-generated client contexts.
    internal static class Program
    {
        // Methods.
        public static async Task<int> Main()
        {
            var failures = VerifyCrypto() + await VerifyJsonDeserializationAsync().ConfigureAwait(false);

            Console.WriteLine(failures == 0 ? "AOT smoke test PASSED" : $"AOT smoke test FAILED ({failures})");
            return failures;
        }

        // Helpers.
        private static int VerifyCrypto()
        {
            try
            {
                var hash = new Hasher().ComputeHash("hello swarm"u8.ToArray());
                if (hash.Length != SwarmHash.HashSize)
                    throw new InvalidOperationException($"Unexpected hash length {hash.Length}.");

                var key = new EthPrivateKey("0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318");
                var signature = key.Sign(new Hasher().ComputeHash("message"u8.ToArray()));
                const int rsvSignatureSize = 65; //r||s||v
                if (signature.Length != rsvSignatureSize)
                    throw new InvalidOperationException($"Unexpected signature length {signature.Length}.");

                Console.WriteLine($"[ok] crypto: keccak + secp256k1 -> address {key.Address}, signature {signature.Length} bytes");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FAIL] crypto: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }

        private static async Task<int> VerifyJsonDeserializationAsync()
        {
            // A NotSupportedException here would mean the JsonTypeInfo metadata was missing (the classic AOT failure mode).
            try
            {
                const string chainStateJson =
                    """{"chainTip":42,"block":40,"totalAmount":"1000","currentPrice":"7","minimumValidityBlocks":3}""";
                // The client refuses an HttpClient whose timeout is shorter than one of its bounds.
                using var client = new SwarmClient(
                    new Uri("http://localhost"),
                    SwarmClients.Bee,
                    new HttpClient(new CannedJsonHandler(chainStateJson)) { Timeout = Timeout.InfiniteTimeSpan });

                var chainState = await client.GetChainStateAsync().ConfigureAwait(false);

                Console.WriteLine($"[ok] json: deserialized ChainState (block {chainState.Block}, tip {chainState.ChainTip})");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FAIL] json: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }
    }
}
