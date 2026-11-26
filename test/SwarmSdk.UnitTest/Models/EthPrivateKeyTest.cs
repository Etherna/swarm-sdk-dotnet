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

using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Models
{
    public class EthPrivateKeyTest
    {
        // Consts.
        private const string PrivateKey = "0x0100000000000000000000000000000000000000000000000000000000000000";
        private const string Address = "dc5b20847f43d67928f49cd4f85d696b5a7617b5"; //of the owner of the replicas

        // Tests.
        [Fact]
        public void AddressIsTheOneOfThePublicKey()
        {
            // Setup.
            var privateKey = new EthPrivateKey(PrivateKey);

            // Assert.
            Assert.Equal(Address, Convert.ToHexStringLower(privateKey.Address.ToByteArray()));
            Assert.Equal(privateKey.PublicKey.ToAddress(), privateKey.Address);
        }

        [Fact]
        public async Task AddressIsTheSameReadFromManyThreads()
        {
            // Setup.
            var privateKey = new EthPrivateKey(PrivateKey);

            // Run.
            var addresses = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => privateKey.Address)));

            // Assert.
            Assert.All(addresses, address => Assert.Equal(Address, Convert.ToHexStringLower(address.ToByteArray())));
        }

        /// <summary>
        /// A derivation multiplies on the curve, and allocates more than a megabyte doing it
        /// </summary>
        [Fact]
        public void AddressIsDerivedOnce()
        {
            // Setup.
            var privateKey = new EthPrivateKey(PrivateKey);
            _ = privateKey.Address;

            // Run.
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            _ = privateKey.Address;
            _ = privateKey.PublicKey;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            // Assert.
            Assert.True(allocated < 1024, $"{allocated} bytes allocated reading the address again");
        }
    }
}
