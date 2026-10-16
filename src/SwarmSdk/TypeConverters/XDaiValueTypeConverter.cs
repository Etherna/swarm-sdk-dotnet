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
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Etherna.SwarmSdk.TypeConverters
{
    public sealed class XDaiValueTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(decimal) ||
            sourceType == typeof(double) ||
            sourceType == typeof(int) ||
            sourceType == typeof(long) ||
            sourceType == typeof(string) ||
            base.CanConvertFrom(context, sourceType);

        public override bool CanConvertTo(ITypeDescriptorContext? context, [NotNullWhen(true)] Type? destinationType) =>
            destinationType == typeof(decimal) ||
            destinationType == typeof(double) ||
            destinationType == typeof(int) ||
            destinationType == typeof(long) ||
            destinationType == typeof(string) ||
            base.CanConvertTo(context, destinationType);

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
            value switch
            {
                decimal bzz => XDaiValue.FromDecimal(bzz),
                double bzz => XDaiValue.FromDouble(bzz),
                int plur => XDaiValue.FromWeiDecimal(plur),
                long plur => XDaiValue.FromWeiDecimal(plur),
                string plur => XDaiValue.FromWeiString(plur),
                _ => base.ConvertFrom(context, culture, value)
            };

        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            if (value is XDaiValue xDaiValue)
            {
                if (destinationType == typeof(decimal))
                    return xDaiValue.ToDecimal();
                if (destinationType == typeof(double))
                    return (double)xDaiValue.ToDecimal();
                if (destinationType == typeof(int))
                    return (int)xDaiValue.ToWeiDecimal();
                if (destinationType == typeof(long))
                    return (long)xDaiValue.ToWeiDecimal();
                if (destinationType == typeof(string))
                    return xDaiValue.ToWeiString();
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}