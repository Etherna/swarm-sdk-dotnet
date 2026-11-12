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

using Etherna.SwarmSdk.Chunks;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Models;
using System;

namespace Etherna.SwarmSdk.Extensions
{
    public static class SwarmCacExtensions
    {
        public static SwarmDecodedCacBase Decode(
            this SwarmCac chunk,
            SwarmReference reference,
            Hasher hasher)
        {
            ArgumentNullException.ThrowIfNull(chunk);
            
            if (chunk.Hash != reference.Hash)
                throw new SwarmChunkVerificationException(
                    reference.Hash,
                    $"Chunk's hash {chunk.Hash} does not match reference {reference}");

            // Read the span, decrypting it alone when the reference carries a key: what it declares is validated
            // before the data is read. Whatever doesn't pass is not a chunk, even if it hashes to the address.
            var span = chunk.Span;
            if (reference.IsEncrypted)
            {
                var spanBuffer = new byte[SwarmCac.SpanSize];
                ChunkEncrypter.DecryptSpan(chunk.Span.Span, reference.EncryptionKey!.Value, spanBuffer, hasher);
                span = spanBuffer;
            }
            var redundancyLevel = SwarmCac.SpanToRedundancyLevel(span.Span);
            var spanLength = SwarmCac.SpanToLength(span.Span);

            //a span larger than a chunk makes it an intermediate one, holding the references of its children: its
            //redundancy level must exist, and some chunk trie must be able to hold its content
            var isDataChunk = spanLength <= SwarmCac.DataSize;
            var (dataShards, parities) = (0, 0);
            if (!isDataChunk)
            {
                if (!Enum.IsDefined(redundancyLevel))
                    throw new SwarmChunkFormatException(
                        reference.Hash,
                        $"Chunk {reference.Hash} span declares the redundancy level {(int)redundancyLevel}, which doesn't exist");
                if (!SwarmCac.TryCountIntermediateReferences(
                        spanLength, redundancyLevel, reference.IsEncrypted, out dataShards, out parities))
                    throw new SwarmChunkFormatException(
                        reference.Hash,
                        $"Chunk {reference.Hash} span of {spanLength} bytes exceeds the content a chunk trie can hold");
            }
            var dataLength = isDataChunk ?
                (int)spanLength :
                SwarmCac.CalculatePlainDataLength(dataShards, parities, reference.IsEncrypted);

            // Read the data, as long as the span implies. An encrypted chunk is always a full one, padded: the
            // padding is cut off after the decryption.
            ReadOnlyMemory<byte> plainData;
            if (reference.IsEncrypted)
            {
                if (chunk.Data.Length != SwarmCac.DataSize)
                    throw new SwarmChunkFormatException(
                        reference.Hash,
                        $"Chunk {reference.Hash} data of {chunk.Data.Length} bytes doesn't fill the chunk, as an encrypted one does");
                var dataBuffer = new byte[SwarmCac.DataSize];
                ChunkEncrypter.DecryptData(chunk.Data.Span, reference.EncryptionKey!.Value, dataBuffer, hasher);
                plainData = dataBuffer.AsMemory(0, dataLength);
            }
            else
            {
                if (chunk.Data.Length != dataLength)
                    throw new SwarmChunkFormatException(
                        reference.Hash,
                        isDataChunk ?
                            $"Chunk {reference.Hash} data of {chunk.Data.Length} bytes is not as long as its span of {spanLength} bytes declares" :
                            $"Chunk {reference.Hash} data of {chunk.Data.Length} bytes doesn't hold the {dataShards} data and {parities} parity references its span of {spanLength} bytes implies");
                plainData = chunk.Data;
            }

            if (isDataChunk)
                return new SwarmDecodedDataCac(reference, spanLength, plainData);
            return new SwarmDecodedIntermediateCac(reference, redundancyLevel, parities, spanLength, plainData);
        }
    }
}