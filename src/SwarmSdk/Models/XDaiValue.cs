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

using Etherna.SwarmSdk.JsonConverters;
using Etherna.SwarmSdk.TypeConverters;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Etherna.SwarmSdk.Models
{
    [JsonConverter(typeof(XDaiValueJsonConverter))]
    [TypeConverter(typeof(XDaiValueTypeConverter))]
    public readonly struct XDaiValue(decimal value) : IEquatable<XDaiValue>, IParsable<XDaiValue>
    {
        // Consts.
        public const int DecimalPrecision = 18;
        public static readonly decimal WeisInXDai = (decimal)Math.Pow(10, DecimalPrecision);

        // Fields.
        private readonly decimal value = decimal.Round(value, DecimalPrecision)
                                         / 1.000000000000000000000000000000000m; //remove final zeros

        // Builders.
        public static XDaiValue FromDecimal(decimal value) => new(value);
        public static XDaiValue FromDouble(double value) => new((decimal)value);
        public static XDaiValue FromInt32(int value) => new(value);
        public static XDaiValue FromInt64(long value) => new(value);
        public static XDaiValue FromString(string value) => new(decimal.Parse(value, CultureInfo.InvariantCulture));
        public static XDaiValue FromWeiDecimal(decimal weiValue) => decimal.Divide(weiValue, WeisInXDai);
        public static XDaiValue FromWeiString(string weiValue) =>
            FromWeiDecimal(decimal.Parse(weiValue, NumberStyles.Integer, CultureInfo.InvariantCulture));

        // Methods.
        public int CompareTo(XDaiValue other) => value.CompareTo(other.value);
        public override bool Equals(object? obj) =>
            obj is XDaiValue xDaiObj &&
            Equals(xDaiObj);
        public bool Equals(XDaiValue other) => value == other.value;
        public override int GetHashCode() => value.GetHashCode();
        public decimal ToDecimal() => value;
        public double ToDouble() => (double)value;
        public override string ToString() => value.ToString(CultureInfo.InvariantCulture);
        public decimal ToWeiDecimal() => decimal.Truncate(decimal.Multiply(value, WeisInXDai));
        public string ToWeiString() => ToWeiDecimal().ToString("F0", CultureInfo.InvariantCulture);

        // Static methods.
        public static XDaiValue Add(XDaiValue left, XDaiValue right) => left + right;
        public static XDaiValue Decrement(XDaiValue value) => --value;
        public static decimal Divide(XDaiValue left, XDaiValue right) => left.value / right.value;
        public static XDaiValue Divide(XDaiValue left, decimal right) => left.value / right;
        public static XDaiValue Divide(XDaiValue left, double right) => left.value / (decimal)right;
        public static XDaiValue Divide(XDaiValue left, int right) => left.value / right;
        public static XDaiValue Divide(XDaiValue left, long right) => left.value / right;
        public static XDaiValue Increment(XDaiValue value) => ++value;
        public static XDaiValue Multiply(XDaiValue left, decimal right) => left.value * right;
        public static XDaiValue Multiply(XDaiValue left, double right) => left.value * (decimal)right;
        public static XDaiValue Multiply(XDaiValue left, int right) => left.value * right;
        public static XDaiValue Multiply(XDaiValue left, long right) => left.value * right;
        public static XDaiValue Multiply(decimal left, XDaiValue right) => left * right.value;
        public static XDaiValue Multiply(double left, XDaiValue right) => (decimal)left * right.value;
        public static XDaiValue Multiply(int left, XDaiValue right) => left * right.value;
        public static XDaiValue Multiply(long left, XDaiValue right) => left * right.value;
        public static XDaiValue Negate(XDaiValue value) => -value;
        public static XDaiValue Parse(string s, IFormatProvider? provider) => FromWeiString(s);
        public static XDaiValue Subtract(XDaiValue left, XDaiValue right) => left - right;
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out XDaiValue result)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                result = default;
                return false;
            }

#pragma warning disable CA1031
            try
            {
                result = FromWeiString(s);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
#pragma warning restore CA1031
        }

        // Operator methods.
        public static XDaiValue operator +(XDaiValue left, XDaiValue right) => left.value + right.value;
        public static XDaiValue operator -(XDaiValue left, XDaiValue right) => left.value - right.value;
        public static XDaiValue operator *(XDaiValue left, decimal right) => left.value * right;
        public static XDaiValue operator *(XDaiValue left, double right) => left.value * (decimal)right;
        public static XDaiValue operator *(XDaiValue left, int right) => left.value * right;
        public static XDaiValue operator *(XDaiValue left, long right) => left.value * right;
        public static XDaiValue operator *(decimal left, XDaiValue right) => left * right.value;
        public static XDaiValue operator *(double left, XDaiValue right) => (decimal)left * right.value;
        public static XDaiValue operator *(int left, XDaiValue right) => left * right.value;
        public static XDaiValue operator *(long left, XDaiValue right) => left * right.value;
        public static decimal operator /(XDaiValue left, XDaiValue right) => left.value / right.value;
        public static XDaiValue operator /(XDaiValue left, decimal right) => left.value / right;
        public static XDaiValue operator /(XDaiValue left, double right) => left.value / (decimal)right;
        public static XDaiValue operator /(XDaiValue left, int right) => left.value / right;
        public static XDaiValue operator /(XDaiValue left, long right) => left.value / right;

        public static bool operator ==(XDaiValue left, XDaiValue right) => left.Equals(right);
        public static bool operator !=(XDaiValue left, XDaiValue right) => !(left == right);
        public static bool operator >(XDaiValue left, XDaiValue right) => left.value > right.value;
        public static bool operator <(XDaiValue left, XDaiValue right) => left.value < right.value;
        public static bool operator >=(XDaiValue left, XDaiValue right) => left.value >= right.value;
        public static bool operator <=(XDaiValue left, XDaiValue right) => left.value <= right.value;
        public static XDaiValue operator -(XDaiValue value) => new(-value.value);
        public static XDaiValue operator ++(XDaiValue value) => new(value.value + 1);
        public static XDaiValue operator --(XDaiValue value) => new(value.value - 1);

        // Implicit conversion operator methods.
        public static implicit operator XDaiValue(decimal value) => new(value);
        public static implicit operator XDaiValue(double value) => new((decimal)value);
        public static implicit operator XDaiValue(int value) => new(value);
        public static implicit operator XDaiValue(long value) => new(value);

        public static explicit operator decimal(XDaiValue value) => value.ToDecimal();
    }
}