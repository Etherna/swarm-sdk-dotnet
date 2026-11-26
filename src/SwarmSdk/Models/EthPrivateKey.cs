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
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Signer;
using System;

namespace Etherna.SwarmSdk.Models
{
    /// <summary>
    /// A secp256k1 private key (32 bytes), able to derive its public key/address and to produce
    /// EIP-191 personal-message signatures.
    /// </summary>
    public sealed class EthPrivateKey
    {
        // Consts.
        public const int KeySize = Secp256k1.PrivateKeySize;

        // Fields.
        private readonly byte[] bytes;
        private readonly Lazy<EthAddress> address;
        private readonly Lazy<EthPublicKey> publicKey;

        // Constructors.
        public EthPrivateKey(byte[] privateKey)
        {
            ArgumentNullException.ThrowIfNull(privateKey);
            if (privateKey.Length != KeySize)
                throw new ArgumentOutOfRangeException(nameof(privateKey));

            bytes = (byte[])privateKey.Clone();
            (publicKey, address) = BuildDerivedKeys(bytes);
        }

        public EthPrivateKey(string privateKey)
        {
            ArgumentNullException.ThrowIfNull(privateKey);

            byte[] parsed;
            try
            {
                parsed = privateKey.HexToByteArray();
            }
            catch (FormatException)
            {
                throw new ArgumentException("Invalid hex", nameof(privateKey));
            }

            if (parsed.Length != KeySize)
                throw new ArgumentOutOfRangeException(nameof(privateKey));

            bytes = parsed;
            (publicKey, address) = BuildDerivedKeys(bytes);
        }

        // Properties.
        /// <summary>
        /// The address of the key, derived at the first read: a derivation multiplies on the curve
        /// </summary>
        public EthAddress Address => address.Value;

        /// <summary>
        /// The public key, derived at the first read: a derivation multiplies on the curve
        /// </summary>
        public EthPublicKey PublicKey => publicKey.Value;

        // Methods.
        /// <summary>
        /// Sign a digest with the Ethereum personal-message prefix (EIP-191, type 0x45).
        /// </summary>
        /// <param name="digest">The digest to sign (the EIP-191 prefix is applied internally)</param>
        /// <param name="hasher">Optional Keccak hasher to reuse; must not be shared across threads</param>
        /// <returns>A 65-byte r||s||v signature</returns>
        public byte[] Sign(ReadOnlySpan<byte> digest, Hasher? hasher = null) => Secp256k1.Sign(digest, bytes, hasher);

        // Helpers.
        /// <summary>
        /// The public key and the address of a private key, each derived once, at its first read from any thread
        /// </summary>
        private static (Lazy<EthPublicKey> PublicKey, Lazy<EthAddress> Address) BuildDerivedKeys(byte[] privateKey)
        {
            var publicKey = new Lazy<EthPublicKey>(() => new EthPublicKey(Secp256k1.GetPublicKey(privateKey)));
            var address = new Lazy<EthAddress>(() => publicKey.Value.ToAddress());
            return (publicKey, address);
        }
    }
}
