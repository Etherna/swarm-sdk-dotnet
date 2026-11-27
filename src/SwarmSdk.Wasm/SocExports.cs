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
using Etherna.SwarmSdk.Models;
using System;
using System.Runtime.InteropServices.JavaScript;

namespace Etherna.SwarmSdk.Wasm
{
    /// <summary>
    /// Keys, single owner chunks and feed updates, exported to JavaScript as
    /// <c>Etherna.SwarmSdk.Wasm.SocExports</c>. Keys, identifiers, topics and addresses are hex strings.
    /// </summary>
    internal static partial class SocExports
    {
        // Methods.
        /// <summary>
        /// The identifier of the update of an epoch feed at the given epoch.
        /// </summary>
        [JSExport]
        internal static string BuildEpochFeedIdentifier(
            string topic,
            [JSMarshalAs<JSType.Number>] long epochStart,
            int epochLevel)
        {
            var hasher = new Hasher();
            return SwarmFeedChunkBase.BuildIdentifier(
                topic,
                new SwarmEpochFeedIndex((ulong)epochStart, (byte)epochLevel, hasher),
                hasher).ToString();
        }

        /// <summary>
        /// The topic of a feed from its name: the Keccak-256 hash of its UTF-8 bytes.
        /// </summary>
        [JSExport]
        internal static string BuildFeedTopic(string name) =>
            new Hasher().ComputeHash(name).ToHex();

        /// <summary>
        /// The identifier of the update of a sequence feed at the given index.
        /// </summary>
        [JSExport]
        internal static string BuildSequenceFeedIdentifier(
            string topic,
            [JSMarshalAs<JSType.Number>] long index) =>
            SwarmFeedChunkBase.BuildIdentifier(
                topic,
                new SwarmSequenceFeedIndex((ulong)index),
                new Hasher()).ToString();

        /// <summary>
        /// Build a single owner chunk wrapping the given data, at most 4096 bytes, and sign it with the
        /// private key of its owner. It returns the full payload of the chunk: identifier, signature, span
        /// and data. A feed update is the chunk at the identifier of its index.
        /// </summary>
        [JSExport]
        internal static byte[] BuildSignedSoc(
            string identifier,
            byte[] data,
            string privateKey)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (data.Length > SwarmCac.DataSize)
                throw new ArgumentOutOfRangeException(nameof(data), $"Data can't exceed {SwarmCac.DataSize} bytes");

            var key = new EthPrivateKey(privateKey);
            var hasher = new Hasher();

            var spanData = new byte[SwarmCac.SpanSize + data.Length];
            SwarmCac.WriteSpan((ulong)data.Length, spanData);
            data.CopyTo(spanData, SwarmCac.SpanSize);
            var innerChunk = new SwarmCac(new SwarmChunkBmt(hasher).Hash(spanData), spanData);

            var soc = new SwarmSoc(identifier, key.Address, innerChunk);
            soc.SignWithPrivateKey(key, hasher);
            return soc.GetFullPayloadToByteArray();
        }

        /// <summary>
        /// The address of a single owner chunk from its identifier and its owner.
        /// </summary>
        [JSExport]
        internal static string BuildSocAddress(string identifier, string owner) =>
            SwarmSoc.BuildHash(identifier, owner, new Hasher()).ToString();

        /// <summary>
        /// The checksummed Ethereum address of a private key.
        /// </summary>
        [JSExport]
        internal static string GetAddressFromPrivateKey(string privateKey) =>
            new EthPrivateKey(privateKey).Address.ToString();
    }
}
