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
    public class XDaiValueTest
    {
        // Consts.
        private const string TenThousandXDaiInWei = "10000000000000000000000"; //1e22 wei, above long.MaxValue (9.2e18)

        // Tests.
        [Fact]
        public void FromWeiStringAcceptsAnAmountAboveLongMaxValue()
        {
            // Run.
            var value = XDaiValue.FromWeiString(TenThousandXDaiInWei);

            // Assert.
            Assert.Equal(10_000m, value.ToDecimal());
            Assert.Equal(TenThousandXDaiInWei, value.ToWeiString());
        }

        [Fact]
        public void FromWeiStringKeepsTheIntegerGrammar()
        {
            // Run & Assert.
            Assert.Equal(XDaiValue.FromWeiDecimal(-12), XDaiValue.FromWeiString(" -12 "));
            Assert.Throws<FormatException>(() => XDaiValue.FromWeiString("1.5"));
            Assert.Throws<FormatException>(() => XDaiValue.FromWeiString("1,000"));
        }

        [Fact]
        public void ParseAndTryParseReadAWeiStringAboveLongMaxValue()
        {
            // Run.
            var parsed = XDaiValue.Parse(TenThousandXDaiInWei, CultureInfo.InvariantCulture);
            var tryParsed = XDaiValue.TryParse(TenThousandXDaiInWei, CultureInfo.InvariantCulture, out var result);

            // Assert.
            Assert.Equal(10_000m, parsed.ToDecimal());
            Assert.True(tryParsed);
            Assert.Equal(parsed, result);
        }

        [Fact]
        public void WeiDecimalRoundTripsAndStaysIntegral()
        {
            // Setup.
            var value = XDaiValue.FromDecimal(1.5m);

            // Run.
            var wei = value.ToWeiDecimal();

            // Assert.
            Assert.Equal(1_500_000_000_000_000_000m, wei);
            Assert.Equal("1500000000000000000", wei.ToString(CultureInfo.InvariantCulture)); //scale 0, no trailing ".0"
            Assert.Equal(value, XDaiValue.FromWeiDecimal(wei));
            Assert.Equal(10_000m, XDaiValue.FromWeiDecimal(10_000_000_000_000_000_000_000m).ToDecimal());
        }

        [Fact]
        public void ToWeiDecimalCastsToLongOnlyWithinItsRange()
        {
            // Setup.
            var value = XDaiValue.FromWeiString(TenThousandXDaiInWei);

            // Run & Assert.
            Assert.Throws<OverflowException>(() => (long)value.ToWeiDecimal());
            Assert.Equal(long.MaxValue, (long)XDaiValue.FromWeiDecimal(long.MaxValue).ToWeiDecimal());
        }

        [Fact]
        public void TypeConverterRoundTripsAWeiStringAboveLongMaxValue()
        {
            // Setup.
            var converter = new XDaiValueTypeConverter();

            // Run.
            var value = converter.ConvertFromInvariantString(TenThousandXDaiInWei);
            var weiString = converter.ConvertToInvariantString(value);

            // Assert.
            Assert.Equal(XDaiValue.FromDecimal(10_000m), Assert.IsType<XDaiValue>(value));
            Assert.Equal(TenThousandXDaiInWei, weiString);
        }
    }
}
