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
using Etherna.SwarmSdk.TypeConverters;
using System;
using System.Globalization;
using Xunit;

namespace Etherna.SwarmSdk.Models
{
    public class BzzValueTest
    {
        // Consts.
        private const string MillionBzzInPlur = "10000000000000000000000"; //1e22 plur, above long.MaxValue (9.2e18)

        // Tests.
        [Fact]
        public void FromPlurStringAcceptsAnAmountAboveLongMaxValue()
        {
            // Run.
            var value = BzzValue.FromPlurString(MillionBzzInPlur);

            // Assert.
            Assert.Equal(1_000_000m, value.ToDecimal());
            Assert.Equal(MillionBzzInPlur, value.ToPlurString());
        }

        [Fact]
        public void FromPlurStringKeepsTheIntegerGrammar()
        {
            // Run & Assert.
            Assert.Equal(BzzValue.FromPlurDecimal(-12), BzzValue.FromPlurString(" -12 "));
            Assert.Throws<FormatException>(() => BzzValue.FromPlurString("1.5"));
            Assert.Throws<FormatException>(() => BzzValue.FromPlurString("1,000"));
        }

        [Fact]
        public void ParseAndTryParseReadAPlurStringAboveLongMaxValue()
        {
            // Run.
            var parsed = BzzValue.Parse(MillionBzzInPlur, CultureInfo.InvariantCulture);
            var tryParsed = BzzValue.TryParse(MillionBzzInPlur, CultureInfo.InvariantCulture, out var result);

            // Assert.
            Assert.Equal(1_000_000m, parsed.ToDecimal());
            Assert.True(tryParsed);
            Assert.Equal(parsed, result);
        }

        [Fact]
        public void PlurDecimalRoundTripsAndStaysIntegral()
        {
            // Setup.
            var value = BzzValue.FromDecimal(1.5m);

            // Run.
            var plur = value.ToPlurDecimal();

            // Assert.
            Assert.Equal(15_000_000_000_000_000m, plur);
            Assert.Equal("15000000000000000", plur.ToString(CultureInfo.InvariantCulture)); //scale 0, no trailing ".0"
            Assert.Equal(value, BzzValue.FromPlurDecimal(plur));
            Assert.Equal(1_000_000m, BzzValue.FromPlurDecimal(10_000_000_000_000_000_000_000m).ToDecimal());
        }

        [Fact]
        public void ToPlurDecimalCastsToLongOnlyWithinItsRange()
        {
            // Setup.
            var value = BzzValue.FromPlurString(MillionBzzInPlur);

            // Run & Assert.
            Assert.Throws<OverflowException>(() => (long)value.ToPlurDecimal());
            Assert.Equal(long.MaxValue, (long)BzzValue.FromPlurDecimal(long.MaxValue).ToPlurDecimal());
        }

        [Fact]
        public void TypeConverterRoundTripsAPlurStringAboveLongMaxValue()
        {
            // Setup.
            var converter = new BzzValueTypeConverter();

            // Run.
            var value = converter.ConvertFromInvariantString(MillionBzzInPlur);
            var plurString = converter.ConvertToInvariantString(value);

            // Assert.
            Assert.Equal(BzzValue.FromDecimal(1_000_000m), Assert.IsType<BzzValue>(value));
            Assert.Equal(MillionBzzInPlur, plurString);
        }
    }
}
