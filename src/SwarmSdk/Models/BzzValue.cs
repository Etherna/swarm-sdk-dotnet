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
    [JsonConverter(typeof(BzzValueJsonConverter))]
    [TypeConverter(typeof(BzzValueTypeConverter))]
    public readonly struct BzzValue(decimal value) : IEquatable<BzzValue>, IParsable<BzzValue>
    {
        // Consts.
        public const int DecimalPrecision = 16;
        public static readonly decimal PlursInBzz = (decimal)Math.Pow(10, DecimalPrecision);

        // Fields.
        private readonly decimal value = decimal.Round(value, DecimalPrecision)
                                         / 1.000000000000000000000000000000000m; //remove final zeros

        // Builders.
        public static BzzValue FromDecimal(decimal value) => new(value);
        public static BzzValue FromDouble(double value) => new((decimal)value);
        public static BzzValue FromInt32(int value) => new(value);
        public static BzzValue FromInt64(long value) => new(value);
        public static BzzValue FromPlurDecimal(decimal plurValue) => decimal.Divide(plurValue, PlursInBzz);
        public static BzzValue FromPlurString(string plurValue) =>
            FromPlurDecimal(decimal.Parse(plurValue, NumberStyles.Integer, CultureInfo.InvariantCulture));
        public static BzzValue FromString(string value) => new(decimal.Parse(value, CultureInfo.InvariantCulture));

        // Methods.
        public int CompareTo(BzzValue other) => value.CompareTo(other.value);
        public override bool Equals(object? obj) =>
            obj is BzzValue xDaiObj &&
            Equals(xDaiObj);
        public bool Equals(BzzValue other) => value == other.value;
        public override int GetHashCode() => value.GetHashCode();
        public decimal ToDecimal() => value;
        public double ToDouble() => (double)value;
        public decimal ToPlurDecimal() => decimal.Truncate(decimal.Multiply(value, PlursInBzz));
        public string ToPlurString() => ToPlurDecimal().ToString("F0", CultureInfo.InvariantCulture);
        public override string ToString() => value.ToString(CultureInfo.InvariantCulture);

        // Static methods.
        public static BzzValue Add(BzzValue left, BzzValue right) => left + right;
        public static BzzValue Decrement(BzzValue value) => --value;
        public static decimal Divide(BzzValue left, BzzValue right) => left.value / right.value;
        public static BzzValue Divide(BzzValue left, decimal right) => left.value / right;
        public static BzzValue Divide(BzzValue left, double right) => left.value / (decimal)right;
        public static BzzValue Divide(BzzValue left, int right) => left.value / right;
        public static BzzValue Divide(BzzValue left, long right) => left.value / right;
        public static BzzValue Increment(BzzValue value) => ++value;
        public static BzzValue Multiply(BzzValue left, decimal right) => left.value * right;
        public static BzzValue Multiply(BzzValue left, double right) => left.value * (decimal)right;
        public static BzzValue Multiply(BzzValue left, int right) => left.value * right;
        public static BzzValue Multiply(BzzValue left, long right) => left.value * right;
        public static BzzValue Multiply(decimal left, BzzValue right) => left * right.value;
        public static BzzValue Multiply(double left, BzzValue right) => (decimal)left * right.value;
        public static BzzValue Multiply(int left, BzzValue right) => left * right.value;
        public static BzzValue Multiply(long left, BzzValue right) => left * right.value;
        public static BzzValue Negate(BzzValue value) => -value;
        public static BzzValue Parse(string s, IFormatProvider? provider) => FromPlurString(s);
        public static BzzValue Subtract(BzzValue left, BzzValue right) => left - right;
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out BzzValue result)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                result = default;
                return false;
            }

#pragma warning disable CA1031
            try
            {
                result = FromPlurString(s);
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
        public static BzzValue operator +(BzzValue left, BzzValue right) => left.value + right.value;
        public static BzzValue operator -(BzzValue left, BzzValue right) => left.value - right.value;
        public static BzzValue operator *(BzzValue left, decimal right) => left.value * right;
        public static BzzValue operator *(BzzValue left, double right) => left.value * (decimal)right;
        public static BzzValue operator *(BzzValue left, int right) => left.value * right;
        public static BzzValue operator *(BzzValue left, long right) => left.value * right;
        public static BzzValue operator *(decimal left, BzzValue right) => left * right.value;
        public static BzzValue operator *(double left, BzzValue right) => (decimal)left * right.value;
        public static BzzValue operator *(int left, BzzValue right) => left * right.value;
        public static BzzValue operator *(long left, BzzValue right) => left * right.value;
        public static decimal operator /(BzzValue left, BzzValue right) => left.value / right.value;
        public static BzzValue operator /(BzzValue left, decimal right) => left.value / right;
        public static BzzValue operator /(BzzValue left, double right) => left.value / (decimal)right;
        public static BzzValue operator /(BzzValue left, int right) => left.value / right;
        public static BzzValue operator /(BzzValue left, long right) => left.value / right;

        public static bool operator ==(BzzValue left, BzzValue right) => left.Equals(right);
        public static bool operator !=(BzzValue left, BzzValue right) => !(left == right);
        public static bool operator >(BzzValue left, BzzValue right) => left.value > right.value;
        public static bool operator <(BzzValue left, BzzValue right) => left.value < right.value;
        public static bool operator >=(BzzValue left, BzzValue right) => left.value >= right.value;
        public static bool operator <=(BzzValue left, BzzValue right) => left.value <= right.value;
        public static BzzValue operator -(BzzValue value) => new(-value.value);
        public static BzzValue operator ++(BzzValue value) => new(value.value + 1);
        public static BzzValue operator --(BzzValue value) => new(value.value - 1);

        // Implicit conversion operator methods.
        public static implicit operator BzzValue(decimal value) => new(value);
        public static implicit operator BzzValue(double value) => new((decimal)value);
        public static implicit operator BzzValue(int value) => new(value);
        public static implicit operator BzzValue(long value) => new(value);

        public static explicit operator decimal(BzzValue value) => value.ToDecimal();
    }
}