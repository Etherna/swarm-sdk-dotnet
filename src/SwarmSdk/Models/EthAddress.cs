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

using Etherna.SwarmSdk.Extensions;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.JsonConverters;
using Etherna.SwarmSdk.TypeConverters;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Etherna.SwarmSdk.Models
{
    [JsonConverter(typeof(EthAddressJsonConverter))]
    [TypeConverter(typeof(EthAddressTypeConverter))]
    public readonly struct EthAddress : IComparable<EthAddress>, IEquatable<EthAddress>, IParsable<EthAddress>
    {
        // Consts.
        public const int AddressSize = 20;
        
        // Fields.
        private readonly byte[] byteAddress;
        
        // Constructors.
        public EthAddress(byte[] address)
        {
            ArgumentNullException.ThrowIfNull(address);
            if (!IsValidAddress(address))
                throw new ArgumentOutOfRangeException(nameof(address));

            byteAddress = address;
        }

        public EthAddress(string address)
        {
            ArgumentNullException.ThrowIfNull(address);
            if (!IsValidAddress(address))
                throw new ArgumentOutOfRangeException(nameof(address));

            byteAddress = address.HexToByteArray();
        }
        
        // Static properties.
        public static EthAddress Zero { get; } = new(new byte[AddressSize]);

        // Methods.
        public int CompareTo(EthAddress other) => byteAddress.AsSpan().SequenceCompareTo(other.byteAddress);
        public bool Equals(EthAddress other) => byteAddress.AsSpan().SequenceEqual(other.byteAddress);
        public override bool Equals(object? obj) => obj is EthAddress other && Equals(other);
        public override int GetHashCode() => byteAddress.AsSpan().ToHashCode();
        public byte[] ToByteArray() => (byte[])byteAddress.Clone();
        public ReadOnlyMemory<byte> ToReadOnlyMemory() => byteAddress.AsMemory();
        public override string ToString() => Secp256k1.ToChecksumAddress(byteAddress);
        public string ToString(bool withHexPrefix)
        {
            var address = ToString();
            if (!withHexPrefix)
                address = address.RemoveHexPrefix();
            return address;
        }
        
        // Static methods.
        public static EthAddress FromByteArray(byte[] value) => new(value);
        public static EthAddress FromString(string value) => new(value);
        public static bool IsValidAddress(byte[] value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return value.Length == AddressSize;
        }
        public static bool IsValidAddress(string value) =>
            //accept as valid both with "0x..." or not
            value.IsHex() &&
            value.RemoveHexPrefix().Length == AddressSize * 2;
        public static EthAddress Parse(string s, IFormatProvider? provider) => FromString(s);
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out EthAddress result)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                result = default;
                return false;
            }

#pragma warning disable CA1031
            try
            {
                result = FromString(s);
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
        public static bool operator ==(EthAddress left, EthAddress right) => left.Equals(right);
        public static bool operator !=(EthAddress left, EthAddress right) => !(left == right);
        public static bool operator <(EthAddress left, EthAddress right) => left.CompareTo(right) < 0;
        public static bool operator <=(EthAddress left, EthAddress right) => left.CompareTo(right) <= 0;
        public static bool operator >(EthAddress left, EthAddress right) => left.CompareTo(right) > 0;
        public static bool operator >=(EthAddress left, EthAddress right) => left.CompareTo(right) >= 0;
        
        // Implicit conversion operator methods.
        public static implicit operator EthAddress(string value) => new(value);
        public static implicit operator EthAddress(byte[] value) => new(value);
        
        // Explicit conversion operator methods.
        public static explicit operator string(EthAddress value) => value.ToString();
        public static explicit operator ReadOnlyMemory<byte>(EthAddress value) => value.ToReadOnlyMemory();
        public static explicit operator byte[](EthAddress value) => value.ToByteArray();
    }
}