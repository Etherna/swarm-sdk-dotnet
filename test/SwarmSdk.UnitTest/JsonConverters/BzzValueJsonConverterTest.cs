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
using Etherna.SwarmSdk.Models;
using System.Text.Json;
using Xunit;

namespace Etherna.SwarmSdk.JsonConverters
{
    public class BzzValueJsonConverterTest
    {
        // Consts.
        private const string MillionBzzInPlur = "10000000000000000000000"; //1e22 plur, above long.MaxValue (9.2e18)
        private static readonly JsonSerializerOptions IntegerOptions = new()
        {
            Converters = { new BzzValueJsonConverter(NumericFormat.AsInteger) }
        };

        // Tests.
        [Fact]
        public void AsIntegerRoundTripsAValueAboveLongMaxValue()
        {
            // Setup.
            var value = BzzValue.FromPlurString(MillionBzzInPlur);

            // Run.
            var json = JsonSerializer.Serialize(value, IntegerOptions);
            var read = JsonSerializer.Deserialize<BzzValue>(json, IntegerOptions);

            // Assert.
            Assert.Equal(MillionBzzInPlur, json);
            Assert.Equal(value, read);
        }

        [Fact]
        public void AsIntegerWritesAnIntegralNumber()
        {
            // Run.
            var json = JsonSerializer.Serialize(BzzValue.FromDecimal(1.5m), IntegerOptions);

            // Assert.
            Assert.Equal("15000000000000000", json);
        }

        [Fact]
        public void AsIntegerReadsALegacyLongPayload()
        {
            // Run.
            var read = JsonSerializer.Deserialize<BzzValue>("9223372036854775807", IntegerOptions);

            // Assert.
            Assert.Equal(BzzValue.FromPlurDecimal(long.MaxValue), read);
        }

        [Fact]
        public void AsIntegerRejectsAFractionalNumber()
        {
            // Run & Assert.
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BzzValue>("1.5", IntegerOptions));
        }

        [Fact]
        public void DefaultConverterRoundTripsAStringAboveLongMaxValue()
        {
            // Setup.
            var value = BzzValue.FromPlurString(MillionBzzInPlur);

            // Run.
            var json = JsonSerializer.Serialize(value);
            var read = JsonSerializer.Deserialize<BzzValue>(json);

            // Assert.
            Assert.Equal($"\"{MillionBzzInPlur}\"", json);
            Assert.Equal(value, read);
        }
    }
}
