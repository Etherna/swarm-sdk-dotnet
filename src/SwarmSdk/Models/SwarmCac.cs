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
using System;
using System.Buffers.Binary;

namespace Etherna.SwarmSdk.Models
{
    public sealed class SwarmCac : SwarmChunk
    {
        // Consts.
        public const int DataSize = 4096;
        public const int SpanDataSize = SpanSize + DataSize;
        public const int SpanSize = 8;
        
        // Constructors.
        public SwarmCac(SwarmHash hash, ReadOnlyMemory<byte> spanData)
        {
            if (spanData.Length < SpanSize)
                throw new ArgumentOutOfRangeException(nameof(spanData),
                    $"Data with span can't be shorter than {SpanSize} bytes");
            if (spanData.Length > SpanDataSize)
                throw new ArgumentOutOfRangeException(nameof(spanData),
                    $"Data with span can't be longer than {SpanDataSize} bytes");
            
            Hash = hash;
            SpanData = spanData;
        }
        
        public SwarmCac(SwarmHash hash, ReadOnlyMemory<byte> span, ReadOnlyMemory<byte> data)
        {
            if (span.Length != SpanSize)
                throw new ArgumentOutOfRangeException(nameof(span), $"Span size must be {SpanSize} bytes");
            if (data.Length > DataSize)
                throw new ArgumentOutOfRangeException(nameof(data), $"Data can't be longer than {DataSize} bytes");
            
            Hash = hash;
            
            var spanDataBuffer = new byte[SpanSize + data.Length];
            span.CopyTo(spanDataBuffer);
            data.CopyTo(spanDataBuffer.AsMemory(SpanSize));
            SpanData = spanDataBuffer;
        }
        
        // Static builders.
        public static SwarmCac BuildFromData(SwarmHash hash, ReadOnlyMemory<byte> data)
        {
            if (data.Length > DataSize)
                throw new ArgumentOutOfRangeException(nameof(data), $"Data can't be longer than {DataSize} bytes");
            
            var spanDataBuffer = new byte[SpanSize + data.Length];
            LengthToSpan((ulong)data.Length).CopyTo(spanDataBuffer, 0);
            data.CopyTo(spanDataBuffer.AsMemory(SpanSize));

            return new(hash, spanDataBuffer);
        }

        // Properties.
        public override SwarmHash Hash { get; }
        public ReadOnlyMemory<byte> Span => SpanData[..SpanSize];
        public ReadOnlyMemory<byte> Data => SpanData[SpanSize..];
        public ReadOnlyMemory<byte> SpanData { get; }

        // Methods.
        public override ReadOnlyMemory<byte> GetFullPayload() => SpanData;
        public override byte[] GetFullPayloadToByteArray() => SpanData.ToArray();

        // Static methods.
        /// <summary>
        /// Plain data length of an intermediate chunk holding the given references: an encrypted data reference
        /// carries its decryption key, a parity reference is always a plain hash
        /// </summary>
        public static int CalculatePlainDataLength(
            int dataShards,
            int parityShards,
            bool encryptedDataReferences) =>
            (encryptedDataReferences ? SwarmReference.EncryptedSize : SwarmReference.PlainSize) * dataShards +
            parityShards * SwarmHash.HashSize;

        public static int CalculatePlainDataLength(
            ulong spanLength,
            RedundancyLevel redundancyLevel,
            bool encryptedDataReferences)
        {
            // If is data chunk.
            if (spanLength <= DataSize)
                return (int)spanLength;
            
            // If is intermediate chunk.
            var (dataShards, parities) = CountIntermediateReferences(spanLength, redundancyLevel, encryptedDataReferences);
            return CalculatePlainDataLength(dataShards, parities, encryptedDataReferences);
        }

        public static (int DataShards, int ParityShards) CountIntermediateReferences(
            ReadOnlySpan<byte> span,
            bool isEncrypted)
        {
            var spanLength = DecodedSpanToLength(IsSpanEncoded(span) ? DecodeSpan(span) : span);
            var redundancyLevel = SpanToRedundancyLevel(span);
            return CountIntermediateReferences(spanLength, redundancyLevel, isEncrypted);
        }
        
        /// <exception cref="OverflowException">No chunk trie can hold a content of the span length</exception>
        public static (int DataShards, int ParityShards) CountIntermediateReferences(
            ulong spanLength,
            RedundancyLevel redundancyLevel,
            bool isEncrypted)
        {
            if (!TryCountIntermediateReferences(spanLength, redundancyLevel, isEncrypted, out var dataShards, out var parityShards))
                throw new OverflowException($"branchSize overflow with spanLength at {spanLength} bytes");
            return (dataShards, parityShards);
        }

        public static ulong DecodedSpanToLength(ReadOnlySpan<byte> span) =>
            BinaryPrimitives.ReadUInt64LittleEndian(span);
        
        /// <summary>
        /// Remove redundancy level from span keeping the real byte count for the chunk
        /// </summary>
        /// <param name="span">Span to decode</param>
        /// <returns>Decoded span</returns>
        public static byte[] DecodeSpan(ReadOnlySpan<byte> span)
        {
            var decodedSpan = new byte[SpanSize];
            span.CopyTo(decodedSpan);
            DecodeSpanInPlace(decodedSpan);
            return decodedSpan;
        }
        
        /// <summary>
        /// Remove redundancy level from span keeping the real byte count for the chunk
        /// </summary>
        /// <param name="span">Span to decode</param>
        public static void DecodeSpanInPlace(Span<byte> span)
        {
            if (span.Length != SpanSize)
                throw new ArgumentException("Span length must be " + SpanSize);

            // Remove redundancy level from the most significant byte.
            span[SpanSize - 1] = 0;
        }

        /// <summary>
        /// Encodes redundancy level into span
        /// </summary>
        /// <param name="span">Span to encode</param>
        /// <param name="level">Redundancy level to encode into span</param>
        /// <returns>Encoded span</returns>
        public static byte[] EncodeSpan(ReadOnlySpan<byte> span, RedundancyLevel level)
        {
            var encodedSpan = new byte[SpanSize];
            span.CopyTo(encodedSpan);
            EncodeSpanInPlace(encodedSpan, level);
            return encodedSpan;
        }

        /// <summary>
        /// Encodes redundancy level into span
        /// </summary>
        /// <param name="span">Span to encode</param>
        /// <param name="level">Redundancy level to encode into span</param>
        public static void EncodeSpanInPlace(Span<byte> span, RedundancyLevel level)
        {
            if (span.Length != SpanSize)
                throw new ArgumentException("Span length must be " + SpanSize);

            // Set redundancy level in the most significant byte.
            span[SpanSize - 1] = (byte)(level + 128);
        }

        public static SwarmShardReference[] GetIntermediateReferencesFromData(
            ReadOnlySpan<byte> data,
            int parityReferencesAmount,
            bool encryptedDataReferences)
        {
            var parityRefLength = SwarmReference.PlainSize;
            var parityRefsLength = parityReferencesAmount * parityRefLength;
            if (data.Length < parityRefsLength)
                throw new ArgumentException("Data can't be shorter than parity references length: " + parityRefsLength);
            
            var dataRefLength = encryptedDataReferences ? SwarmReference.EncryptedSize : SwarmReference.PlainSize;
            var dataRefsLength = data.Length - parityRefsLength;
            if (dataRefsLength % dataRefLength != 0)
                throw new ArgumentException($"Data length must be multiple of {dataRefLength}");

            var dataReferencesAmount = dataRefsLength / dataRefLength;
            var references = new SwarmShardReference[dataReferencesAmount + parityReferencesAmount];
            var cursor = 0;
            for (int i = 0; i < references.Length; i++)
            {
                if (i < dataReferencesAmount) //data reference
                {
                    references[i] = new SwarmShardReference(data[cursor..(cursor + dataRefLength)].ToArray(), false);
                    cursor += dataRefLength;
                }
                else //parity reference
                {
                    references[i] = new SwarmShardReference(data[cursor..(cursor + parityRefLength)].ToArray(), true);
                    cursor += parityRefLength;
                }
            }

            return references;
        }

        public static SwarmShardReference[] GetIntermediateReferencesFromSpanData(
            ReadOnlySpan<byte> span,
            ReadOnlySpan<byte> data,
            bool encryptedDataReferences)
        {
            var spanLength = DecodedSpanToLength(IsSpanEncoded(span) ? DecodeSpan(span) : span);
            var redundancyLevel = SpanToRedundancyLevel(span);
            var (_, parities) = CountIntermediateReferences(spanLength, redundancyLevel, encryptedDataReferences);
            return GetIntermediateReferencesFromData(data, parities, encryptedDataReferences);
        }

        public static bool IsSpanEncoded(ReadOnlySpan<byte> span)
        {
            if (span.Length != SpanSize)
                throw new ArgumentException("Span length must be " + SpanSize);
            
            return span[SpanSize - 1] > 128;
        }
        
        public static bool IsValid(SwarmHash hash, ReadOnlyMemory<byte> spanData, SwarmChunkBmt swarmChunkBmt)
        {
            ArgumentNullException.ThrowIfNull(swarmChunkBmt);

            if (spanData.Length is < SpanSize or > SpanDataSize)
                return false;
            return hash == swarmChunkBmt.Hash(spanData);
        }
        
        public static byte[] LengthToSpan(ulong length)
        {
            var span = new byte[SpanSize];
            WriteSpan(length, span);
            return span;
        }

        public static ulong SpanToLength(ReadOnlySpan<byte> span) =>
            DecodedSpanToLength(IsSpanEncoded(span) ?
                DecodeSpan(span) :
                span);
        
        public static RedundancyLevel SpanToRedundancyLevel(ReadOnlySpan<byte> span)
        {
            if (span.Length != SpanSize)
                throw new ArgumentException("Span length must be " + SpanSize);
            
            if (!IsSpanEncoded(span))
                return RedundancyLevel.None;
            return (RedundancyLevel)(span[SpanSize - 1] & 127);
        }

        /// <summary>
        /// Count the data and parity references an intermediate chunk of the given span holds
        /// </summary>
        /// <returns>False when no chunk trie can hold a content of the span length</returns>
        public static bool TryCountIntermediateReferences(
            ulong spanLength,
            RedundancyLevel redundancyLevel,
            bool isEncrypted,
            out int dataShards,
            out int parityShards)
        {
            if (spanLength <= DataSize)
                throw new ArgumentOutOfRangeException(nameof(spanLength), $"Span length is not from intermediate chunk, it must be greater than {DataSize} bytes");
            
            /*
             * Assume we have a trie of size `span` then we can assume that all the forks except for
             * the last one on the right are of equal size, this is due to how the splitter wraps levels.
             * First the algorithm will search for a BMT level where span can be included,
             * then identify how large data one reference can hold on that level,
             * then count how many references can satisfy span,
             * and finally how many parity shards should be on that level.
             */
            
            //branching factor is how many data shard references can fit into one intermediate chunk
            var branching = (ulong)redundancyLevel.GetMaxDataShards(isEncrypted);
            
            // Search for branch level big enough to include span.
            var branchLevel = 1;
            ulong branchSize = DataSize;
            for (; branchSize < spanLength; branchLevel++)
            {
                var high = Math.BigMul(branchSize, branching, out branchSize);
                if (high != 0) //overflow: no chunk trie holds this span
                {
                    dataShards = 0;
                    parityShards = 0;
                    return false;
                }
            }
            
            // Span in one full reference. referenceSize = branching ^ (branchLevel - 1)
            ulong referenceSize = DataSize;
            for (var i = 1; i < branchLevel - 1; i++)
                referenceSize *= branching;

            dataShards = (int)(spanLength / referenceSize);
            if (spanLength % referenceSize != 0)
                dataShards++;

            parityShards = redundancyLevel.GetParitiesAmount(isEncrypted, dataShards);

            return true;
        }

        public static void WriteSpan(ulong length, Span<byte> outputSpan) =>
            BinaryPrimitives.WriteUInt64LittleEndian(outputSpan, length);
    }
}