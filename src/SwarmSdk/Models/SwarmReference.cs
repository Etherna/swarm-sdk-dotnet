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
using Etherna.SwarmSdk.JsonConverters;
using Etherna.SwarmSdk.TypeConverters;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Etherna.SwarmSdk.Models
{
    [JsonConverter(typeof(SwarmReferenceJsonConverter))]
    [TypeConverter(typeof(SwarmReferenceTypeConverter))]
    public readonly struct SwarmReference : IEquatable<SwarmReference>, IParsable<SwarmReference>
    {
        // Consts.
        public const int EncryptedSize = SwarmHash.HashSize + EncryptionKey256.KeySize;
        public const int PlainSize = SwarmHash.HashSize;
        
        // Fields.
        private readonly ReadOnlyMemory<byte> byteReference;
        
        // Constructors.
        public SwarmReference(ReadOnlyMemory<byte> reference)
        {
            if (!IsValidReference(reference))
                throw new ArgumentOutOfRangeException(nameof(reference));
            
            byteReference = reference;
        }

        public SwarmReference(string reference)
        {
            ArgumentNullException.ThrowIfNull(reference);
            
            try
            {
                byteReference = reference.HexToByteArray();
            }
            catch (FormatException)
            {
                throw new ArgumentException("Invalid reference", nameof(reference));
            }
            
            if (!IsValidReference(byteReference))
                throw new ArgumentOutOfRangeException(nameof(reference));
        }
        
        public SwarmReference(
            SwarmHash hash,
            EncryptionKey256? encryptionKey)
        {
            if (encryptionKey.HasValue)
            {
                var bytes = new byte[EncryptedSize];
                hash.ToReadOnlyMemory().CopyTo(bytes.AsMemory()[..PlainSize]);
                encryptionKey.Value.ToReadOnlyMemory().CopyTo(bytes.AsMemory()[PlainSize..]);
                byteReference = bytes;
            }
            else
            {
                byteReference = hash.ToReadOnlyMemory();
            }
        }
        
        // Properties.
        public EncryptionKey256? EncryptionKey =>
            IsEncrypted ? new EncryptionKey256(byteReference[PlainSize..]) : (EncryptionKey256?)null;
        public SwarmHash Hash => new(byteReference[..PlainSize]);
        public bool IsEncrypted => byteReference.Length == EncryptedSize;
        public int Size => byteReference.Length;
        
        // Static properties.
        public static SwarmReference EncryptedZero { get; } = new byte[EncryptedSize];
        public static SwarmReference PlainZero { get; } = new byte[PlainSize];
        
        // Methods.
        public bool Equals(SwarmReference other) => byteReference.Span.SequenceEqual(other.byteReference.Span);
        public override bool Equals(object? obj) => obj is SwarmReference other && Equals(other);
        public override int GetHashCode() => byteReference.Span.ToHashCode();
        public byte[] ToByteArray() => byteReference.ToArray();
        public ReadOnlyMemory<byte> ToReadOnlyMemory() => byteReference;
        public override string ToString() => byteReference.ToArray().ToHex();
        
        // Static methods.
        public static SwarmReference FromByteArray(byte[] value) => new(value);
        public static SwarmReference FromString(string value) => new(value);
        public static SwarmReference FromSwarmHash(SwarmHash hash) => new(hash, null);
        public static bool IsValidReference(ReadOnlyMemory<byte> value) => value.Length is PlainSize or EncryptedSize;
        public static bool IsValidReference(string value)
        {
            try
            {
                return IsValidReference(value.HexToByteArray());
            }
            catch (FormatException)
            {
                return false;
            }
        }
        public static bool IsZero(SwarmReference value) =>
            value.IsEncrypted ? value == EncryptedZero : value == PlainZero;
        public static SwarmReference Parse(string s, IFormatProvider? provider) => FromString(s);
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SwarmReference result)
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
        public static bool operator ==(SwarmReference left, SwarmReference right) => left.Equals(right);
        public static bool operator !=(SwarmReference left, SwarmReference right) => !(left == right);
        
        // Implicit conversion operator methods.
        public static implicit operator SwarmReference(string value) => new(value);
        public static implicit operator SwarmReference(byte[] value) => new(value);
        public static implicit operator SwarmReference(SwarmHash hash) => new(hash, null);
        
        // Explicit conversion operator methods.
        public static explicit operator string(SwarmReference value) => value.ToString();
        public static explicit operator ReadOnlyMemory<byte>(SwarmReference value) => value.ToReadOnlyMemory();
        public static explicit operator byte[](SwarmReference value) => value.ToByteArray();
    }
}