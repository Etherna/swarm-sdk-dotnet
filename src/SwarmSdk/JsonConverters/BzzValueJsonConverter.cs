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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Etherna.SwarmSdk.JsonConverters
{
    public sealed class BzzValueJsonConverter(NumericFormat valueFormat)
        : JsonConverter<BzzValue>
    {
        public BzzValueJsonConverter()
            : this(NumericFormat.AsString)
        { }

        public override BzzValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.Number when valueFormat == NumericFormat.AsFloat =>
                    BzzValue.FromDouble(reader.GetDouble()),
                JsonTokenType.Number when valueFormat == NumericFormat.AsInteger =>
                    reader.GetDecimal() switch
                    {
                        var plur when plur == decimal.Truncate(plur) => BzzValue.FromPlurDecimal(plur),
                        _ => throw new JsonException("The integer format requires an integral number")
                    },
                JsonTokenType.String => BzzValue.FromPlurString(reader.GetString()!),
                _ => throw new JsonException()
            };

        public override void Write(Utf8JsonWriter writer, BzzValue value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            switch (valueFormat)
            {
                case NumericFormat.AsFloat:
                    writer.WriteNumberValue(value.ToDouble());
                    break;
                case NumericFormat.AsInteger:
                    writer.WriteNumberValue(value.ToPlurDecimal()); //integral with scale 0: no fractional digits, no range cap
                    break;
                case NumericFormat.AsString:
                    writer.WriteStringValue(value.ToPlurString());
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported format: {valueFormat}");
            }
        }
    }
}