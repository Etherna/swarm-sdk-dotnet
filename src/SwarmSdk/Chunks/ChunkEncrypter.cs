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
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Etherna.SwarmSdk.Chunks
{
    public static class ChunkEncrypter
    {
        // Methods.
        /// <summary>
        /// Decrypt a chunk
        /// </summary>
        /// <param name="chunk">Encrypted chunk</param>
        /// <param name="key">Encryption key</param>
        /// <param name="decryptedSpan">Output buffer for decrypted span</param>
        /// <param name="decryptedData">Output buffer for decrypted data</param>
        /// <param name="hasher">Hasher instance</param>
        /// <returns>Decrypted chunk data length</returns>
        public static int DecryptChunk(
            SwarmCac chunk,
            EncryptionKey256 key,
            Span<byte> decryptedSpan,
            Span<byte> decryptedData,
            Hasher hasher)
        {
            ArgumentNullException.ThrowIfNull(chunk);

            return DecryptChunk(
                chunk.Span.Span,
                chunk.Data.Span,
                key,
                decryptedSpan,
                decryptedData,
                hasher);
        }

        /// <summary>
        /// Decrypt a chunk
        /// </summary>
        /// <param name="chunk">Encrypted chunk</param>
        /// <param name="key">Encryption key</param>
        /// <param name="hasher">Hasher instance</param>
        /// <param name="decryptedSpanData">Output decrypted spanData</param>
        public static void DecryptChunk(
            SwarmCac chunk,
            EncryptionKey256 key,
            Hasher hasher,
            out ReadOnlyMemory<byte> decryptedSpanData)
        {
            ArgumentNullException.ThrowIfNull(chunk);

            var decryptedSpanDataArray = new byte[SwarmCac.SpanDataSize];
            var dataLength = DecryptChunk(
                chunk.Span.Span,
                chunk.Data.Span,
                key,
                decryptedSpanDataArray.AsSpan()[..SwarmCac.SpanSize],
                decryptedSpanDataArray.AsSpan()[SwarmCac.SpanSize..],
                hasher);

            decryptedSpanData = decryptedSpanDataArray.AsMemory()[..(SwarmCac.SpanSize + dataLength)];
        }

        /// <summary>
        /// Decrypt a chunk
        /// </summary>
        /// <param name="chunkSpan">Encrypted chunk span</param>
        /// <param name="chunkData">Encrypted chunk data</param>
        /// <param name="key">Encryption key</param>
        /// <param name="decryptedSpan">Output buffer for decrypted span</param>
        /// <param name="decryptedData">Output buffer for decrypted data</param>
        /// <param name="hasher">Hasher instance</param>
        /// <returns>Decrypted chunk data length</returns>
        public static int DecryptChunk(
            ReadOnlySpan<byte> chunkSpan,
            ReadOnlySpan<byte> chunkData,
            EncryptionKey256 key,
            Span<byte> decryptedSpan,
            Span<byte> decryptedData,
            Hasher hasher)
        {
            if (decryptedSpan.Length != SwarmCac.SpanSize)
                throw new ArgumentException($"{nameof(decryptedSpan)} must have size {SwarmCac.SpanSize}");
            if (decryptedData.Length != SwarmCac.DataSize)
                throw new ArgumentException($"{nameof(decryptedData)} must have size {SwarmCac.DataSize}");

            // Decrypt.
            DecryptSpan(chunkSpan, key, decryptedSpan, hasher);
            DecryptData(chunkData, key, decryptedData, hasher);

            // Calculate real data length removing added padding.
            var level = SwarmCac.SpanToRedundancyLevel(decryptedSpan);
            var decodedSpan = SwarmCac.DecodeSpan(decryptedSpan);
            var length = SwarmCac.DecodedSpanToLength(decodedSpan);
            return SwarmCac.CalculatePlainDataLength(length, level, true);
        }

        /// <summary>
        /// Decrypt a chunk
        /// </summary>
        /// <param name="chunkSpan">Encrypted chunk span</param>
        /// <param name="chunkData">Encrypted chunk data</param>
        /// <param name="key">Encryption key</param>
        /// <param name="hasher">Hasher instance</param>
        /// <param name="decryptedSpanData">Output decrypted spanData</param>
        public static void DecryptChunk(
            ReadOnlySpan<byte> chunkSpan,
            ReadOnlySpan<byte> chunkData,
            EncryptionKey256 key,
            Hasher hasher,
            out ReadOnlyMemory<byte> decryptedSpanData)
        {
            var decryptedSpanDataArray = new byte[SwarmCac.SpanDataSize];
            var dataLength = DecryptChunk(
                chunkSpan,
                chunkData,
                key,
                decryptedSpanDataArray.AsSpan()[..SwarmCac.SpanSize],
                decryptedSpanDataArray.AsSpan()[SwarmCac.SpanSize..],
                hasher);

            decryptedSpanData = decryptedSpanDataArray.AsMemory()[..(SwarmCac.SpanSize + dataLength)];
        }

        /// <summary>
        /// Encrypt a chunk
        /// </summary>
        /// <param name="chunk">Source chunk to encrypt</param>
        /// <param name="key">Encryption key, can be null to create a new random key</param>
        /// <param name="encryptedSpan">Output buffer for encrypted span</param>
        /// <param name="encryptedData">Output buffer for encrypted data</param>
        /// <param name="hasher">Hasher instance</param>
        /// <param name="paddingBytes">Optional custom padding bytes. Generate random if null</param>
        /// <returns>The encryption key used</returns>
        public static EncryptionKey256 EncryptChunk(
            SwarmCac chunk,
            EncryptionKey256? key,
            Span<byte> encryptedSpan,
            Span<byte> encryptedData,
            Hasher hasher,
            ReadOnlyMemory<byte>? paddingBytes = null)
        {
            ArgumentNullException.ThrowIfNull(chunk);

            return EncryptChunk(
                chunk.Span.Span,
                chunk.Data.Span,
                key,
                encryptedSpan,
                encryptedData,
                hasher,
                paddingBytes);
        }

        /// <summary>
        /// Encrypt a chunk
        /// </summary>
        /// <param name="chunk">Source chunk to encrypt</param>
        /// <param name="key">Encryption key, can be null to create a new random key</param>
        /// <param name="hasher">Hasher instance</param>
        /// <param name="encryptedSpanData">Output encrypted spanData</param>
        /// <param name="paddingBytes">Optional custom padding bytes. Generate random if null</param>
        /// <returns>The encryption key used</returns>
        public static EncryptionKey256 EncryptChunk(
            SwarmCac chunk,
            EncryptionKey256? key,
            Hasher hasher,
            out byte[] encryptedSpanData,
            ReadOnlyMemory<byte>? paddingBytes = null)
        {
            ArgumentNullException.ThrowIfNull(chunk);

            encryptedSpanData = new byte[SwarmCac.SpanDataSize];
            return EncryptChunk(
                chunk.Span.Span,
                chunk.Data.Span,
                key,
                encryptedSpanData.AsSpan()[..SwarmCac.SpanSize],
                encryptedSpanData.AsSpan()[SwarmCac.SpanSize..],
                hasher,
                paddingBytes);
        }

        /// <summary>
        /// Encrypt a chunk
        /// </summary>
        /// <param name="chunkSpan">Source chunk span to encrypt</param>
        /// <param name="chunkData">Source chunk data to encrypt</param>
        /// <param name="key">Encryption key, can be null to create a new random key</param>
        /// <param name="encryptedSpan">Output buffer for encrypted span</param>
        /// <param name="encryptedData">Output buffer for encrypted data</param>
        /// <param name="hasher">Hasher instance</param>
        /// <param name="paddingBytes">Optional custom padding bytes. Generate random if null</param>
        /// <returns>The encryption key used</returns>
        public static EncryptionKey256 EncryptChunk(
            ReadOnlySpan<byte> chunkSpan,
            ReadOnlySpan<byte> chunkData,
            EncryptionKey256? key,
            Span<byte> encryptedSpan,
            Span<byte> encryptedData,
            Hasher hasher,
            ReadOnlyMemory<byte>? paddingBytes = null)
        {
            if (encryptedSpan.Length != SwarmCac.SpanSize)
                throw new ArgumentException($"{nameof(encryptedSpan)} must have size {SwarmCac.SpanSize}");
            if (encryptedData.Length != SwarmCac.DataSize)
                throw new ArgumentException($"{nameof(encryptedData)} must have size {SwarmCac.DataSize}");

            key ??= EncryptionKey256.BuildNewRandom();

            Transform(chunkSpan, key.Value, SwarmCac.DataSize / EncryptionKey256.KeySize, encryptedSpan, hasher, paddingBytes);
            Transform(chunkData, key.Value, 0, encryptedData, hasher, paddingBytes);

            return key.Value;
        }

        /// <summary>
        /// Encrypt a chunk
        /// </summary>
        /// <param name="chunkSpan">Source chunk span to encrypt</param>
        /// <param name="chunkData">Source chunk data to encrypt</param>
        /// <param name="key">Encryption key, can be null to create a new random key</param>
        /// <param name="hasher">Hasher instance</param>
        /// <param name="encryptedSpanData">Output encrypted spanData</param>
        /// <param name="paddingBytes">Optional custom padding bytes. Generate random if null</param>
        /// <returns>The encryption key used</returns>
        public static EncryptionKey256 EncryptChunk(
            ReadOnlySpan<byte> chunkSpan,
            ReadOnlySpan<byte> chunkData,
            EncryptionKey256? key,
            Hasher hasher,
            out byte[] encryptedSpanData,
            ReadOnlyMemory<byte>? paddingBytes = null)
        {
            encryptedSpanData = new byte[SwarmCac.SpanDataSize];
            return EncryptChunk(
                chunkSpan,
                chunkData,
                key,
                encryptedSpanData.AsSpan()[..SwarmCac.SpanSize],
                encryptedSpanData.AsSpan()[SwarmCac.SpanSize..],
                hasher,
                paddingBytes);
        }

        // Internals.
        /// <summary>
        /// Decrypt the data of a chunk, keeping its padding
        /// </summary>
        internal static void DecryptData(
            ReadOnlySpan<byte> chunkData,
            EncryptionKey256 key,
            Span<byte> decryptedData,
            Hasher hasher) =>
            Transform(chunkData, key, 0, decryptedData, hasher, null);

        /// <summary>
        /// Decrypt the span of a chunk
        /// </summary>
        internal static void DecryptSpan(
            ReadOnlySpan<byte> chunkSpan,
            EncryptionKey256 key,
            Span<byte> decryptedSpan,
            Hasher hasher) =>
            Transform(chunkSpan, key, SwarmCac.DataSize / EncryptionKey256.KeySize, decryptedSpan, hasher, null);

        // Helpers.
        private static void Transform(
            ReadOnlySpan<byte> input,
            EncryptionKey256 key,
            uint initCtr,
            Span<byte> output,
            Hasher hasher,
            ReadOnlyMemory<byte>? paddingBytes)
        {
            var index = 0;

            var inputLength = input.Length;
            for (var i = 0; i < inputLength; i += EncryptionKey256.KeySize)
            {
                var l = Math.Min(EncryptionKey256.KeySize, inputLength - i);
                Transcript(input[i..(i + l)], key, index, initCtr, output[i..(i + l)], hasher, paddingBytes);
                index++;
            }

            // Pad the rest if output is longer.
            if (paddingBytes.HasValue)
                paddingBytes.Value.Span.Fill(output[inputLength..]);
            else
                RandomNumberGenerator.Fill(output[inputLength..]);
        }

        private static void Transcript(
            ReadOnlySpan<byte> input,
            EncryptionKey256 key,
            int index,
            uint initCtr,
            Span<byte> output,
            Hasher hasher,
            ReadOnlyMemory<byte>? paddingBytes)
        {
            // First hash key with counter (initial counter + index).
            var ctrBytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(ctrBytes, (uint)index + initCtr);
            var ctrHash = hasher.ComputeHash([key.ToReadOnlyMemory(), ctrBytes]);

            // Second round of hashing for selective disclosure.
            var segmentKey = hasher.ComputeHash(ctrHash);

            // XOR input with segmentKey.
            var inputLength = input.Length;
            for (var i = 0; i < inputLength; i++)
                output[i] = (byte)(input[i] ^ segmentKey[i]);

            // Pad the rest if output is longer.
            if (paddingBytes.HasValue)
                paddingBytes.Value.Span.Fill(output[inputLength..]);
            else
                RandomNumberGenerator.Fill(output[inputLength..]);
        }
    }
}