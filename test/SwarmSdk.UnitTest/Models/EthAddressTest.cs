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
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Etherna.SwarmSdk.Models
{
    public class EthAddressTest
    {
        // Data.
        // Canonical EIP-55 checksum test vectors (see https://eips.ethereum.org/EIPS/eip-55).
        public static IEnumerable<object[]> Eip55ChecksumVectors =>
            new[]
            {
                "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed",
                "0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359",
                "0xdbF03B407c01E7cD3CBea99509d93f8DDDC8C6FB",
                "0xD1220A0cf47c7B9Be7A2E6BA89F429762e7b9aDb"
            }.Select(checksummed => new object[] { checksummed });

        // Tests.
        [Theory, MemberData(nameof(Eip55ChecksumVectors))]
        public void ToStringAppliesEip55Checksum(string checksummed)
        {
            // Build from the all-lowercase form, without prefix.
            var lowercase = checksummed[2..].ToLowerInvariant();

            var address = new EthAddress(lowercase);

            Assert.Equal(checksummed, address.ToString());
        }

        [Theory, MemberData(nameof(Eip55ChecksumVectors))]
        public void ConstructorAcceptsAnyCasingAndWithOrWithoutPrefix(string checksummed)
        {
            var fromChecksummed = new EthAddress(checksummed);
            var fromLowercaseNoPrefix = new EthAddress(checksummed[2..].ToLowerInvariant());
            var fromUppercaseNoPrefix = new EthAddress(checksummed[2..].ToUpperInvariant());

            Assert.Equal(fromChecksummed, fromLowercaseNoPrefix);
            Assert.Equal(fromChecksummed, fromUppercaseNoPrefix);
            Assert.Equal(checksummed, fromChecksummed.ToString());
        }

        [Fact]
        public void ConstructorFromBytesRoundTrips()
        {
            var bytes = Enumerable.Range(0, EthAddress.AddressSize).Select(i => (byte)i).ToArray();

            var address = new EthAddress(bytes);

            Assert.Equal(bytes, address.ToByteArray());
        }

        [Fact]
        public void ToByteArrayReturnsDefensiveCopy()
        {
            var bytes = Enumerable.Range(0, EthAddress.AddressSize).Select(i => (byte)i).ToArray();
            var address = new EthAddress(bytes);

            var exported = address.ToByteArray();
            exported[0] = 0xFF;

            Assert.Equal((byte)0, address.ToByteArray()[0]);
        }

        [Fact]
        public void ToStringWithoutPrefixOmitsLeading0x()
        {
            var address = new EthAddress("5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed");

            Assert.Equal("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", address.ToString(true));
            Assert.Equal("5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", address.ToString(false));
        }

        [Fact]
        public void ZeroIsTwentyZeroBytes()
        {
            Assert.Equal(new byte[EthAddress.AddressSize], EthAddress.Zero.ToByteArray());
            Assert.Equal("0x0000000000000000000000000000000000000000", EthAddress.Zero.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-hex-at-all")]
        [InlineData("0x123")]                                          // too short
        [InlineData("5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAedAA")]    // too long
        public void ConstructorRejectsInvalidStrings(string value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EthAddress(value));
        }

        [Theory]
        [InlineData(19)]
        [InlineData(21)]
        public void ConstructorRejectsInvalidByteLength(int length)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EthAddress(new byte[length]));
        }

        [Fact]
        public void EqualityIsByValue()
        {
            var a = new EthAddress("5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed");
            var b = new EthAddress("5aaeb6053f3e94c9b9a09f33669435e7ef1beaed");
            var c = new EthAddress("dbF03B407c01E7cD3CBea99509d93f8DDDC8C6FB");

            Assert.True(a == b);
            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.True(a != c);
            Assert.False(a.Equals(c));
        }

        [Fact]
        public void ComparisonIsByAddressBytes()
        {
            var a = new EthAddress("0x0000000000000000000000000000000000000001");
            var b = new EthAddress("0x0000000000000000000000000000000000000002");
            var c = new EthAddress("0x0100000000000000000000000000000000000000");

            Assert.True(a.CompareTo(b) < 0);
            Assert.True(b.CompareTo(a) > 0);
            Assert.Equal(0, a.CompareTo(new EthAddress("0x0000000000000000000000000000000000000001")));
            Assert.True(a < b);
            Assert.True(a <= b);
            Assert.True(c > b);
            Assert.True(c >= b);
        }

        [Fact]
        public void OrderingByAddressUsesTheDefaultComparer()
        {
            var a = new EthAddress("0x0000000000000000000000000000000000000001");
            var b = new EthAddress("0x0000000000000000000000000000000000000002");
            var c = new EthAddress("0x0100000000000000000000000000000000000000");

            //an EthAddress key compiles without IComparable, and the sort threw at runtime without it
            var ordered = new[] { c, a, b }.OrderBy(address => address).ToArray();

            Assert.Equal([a, b, c], ordered);
        }

        [Fact]
        public void TryParseHandlesValidAndInvalid()
        {
            Assert.True(EthAddress.TryParse("5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", null, out var parsed));
            Assert.Equal("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", parsed.ToString());

            Assert.False(EthAddress.TryParse(null, null, out _));
            Assert.False(EthAddress.TryParse("   ", null, out _));
            Assert.False(EthAddress.TryParse("nope", null, out _));
        }

        [Fact]
        public void ConversionOperatorsRoundTrip()
        {
            var bytes = Enumerable.Range(0, EthAddress.AddressSize).Select(i => (byte)i).ToArray();

            EthAddress fromBytes = bytes;
            EthAddress fromString = "5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed";

            Assert.Equal(bytes, (byte[])fromBytes);
            Assert.Equal("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", (string)fromString);
        }
    }
}
