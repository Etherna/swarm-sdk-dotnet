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

using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Models;
using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;
using Xunit;

namespace Etherna.SwarmSdk.Extensions
{
    public class SwarmCacExtensionsTest
    {
        // Tests.
        [Fact]
        public void DecodeRejectsAChunkOfAnotherReference()
        {
            // Setup.
            var bmt = new SwarmChunkBmt();
            var data = Enumerable.Range(0, 42).Select(i => (byte)i).ToArray();
            var spanData = SwarmCac.BuildFromData(SwarmHash.Zero, data).SpanData;
            var chunk = new SwarmCac(bmt.Hash(spanData), spanData);
            SwarmReference anotherReference = "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9";
            
            // Run.
            var thrown = Assert.Throws<SwarmChunkVerificationException>(() => chunk.Decode(anotherReference, new Hasher()));
            
            // Assert.
            Assert.Equal(anotherReference.Hash, thrown.Hash);
        }

        [Fact]
        public void DecodeRejectsAnIntermediateChunkNotHoldingTheReferencesOfItsSpan()
        {
            // Setup.
            //a span of two data chunks over a payload of three references
            var bmt = new SwarmChunkBmt();
            var spanData = new byte[SwarmCac.SpanSize + 3 * SwarmReference.PlainSize];
            BinaryPrimitives.WriteUInt64LittleEndian(spanData, SwarmCac.DataSize + 1);
            var chunk = new SwarmCac(bmt.Hash(spanData), spanData);

            // Run.
            var thrown = Assert.Throws<SwarmChunkFormatException>(() =>
                chunk.Decode(new SwarmReference(chunk.Hash, null), new Hasher()));

            // Assert.
            Assert.Equal(chunk.Hash, thrown.Hash);
            Assert.Contains("96 bytes", thrown.Message, StringComparison.Ordinal);
            Assert.Contains("2 data", thrown.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void DecodeRejectsAChunkWhoseSpanNoTrieCanHold()
        {
            // Setup.
            //arbitrary bytes read as a chunk: the first eight are a span of about 2^62 bytes
            var bmt = new SwarmChunkBmt();
            var spanData = Encoding.UTF8.GetBytes("second update of the feed, written as plain text");
            var chunk = new SwarmCac(bmt.Hash(spanData), spanData);

            // Run.
            var thrown = Assert.Throws<SwarmChunkFormatException>(() =>
                chunk.Decode(new SwarmReference(chunk.Hash, null), new Hasher()));

            // Assert.
            Assert.Equal(chunk.Hash, thrown.Hash);
            Assert.Contains("exceeds the content a chunk trie can hold", thrown.Message, StringComparison.Ordinal);
        }
    }
}
