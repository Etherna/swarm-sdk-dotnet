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
using Etherna.SwarmSdk.Hashing.Signer;
using System;
using System.Linq;
using Xunit;

namespace Etherna.SwarmSdk.Models
{
    public class SwarmChunkTest
    {
        // Consts.
        private const string PrivateKeyHex = "0x4f3edf983ac636a65a842ce7c78d9aa706d3b113bce9c46f30d7d21715b23b1d";
        
        // Tests.
        [Fact]
        public void BuildChunkFromHashAndDataBuildsCacOnMatchingHash()
        {
            // Setup.
            var bmt = new SwarmChunkBmt();
            var spanData = BuildInnerChunk(bmt).SpanData;
            var hash = bmt.Hash(spanData);
            
            // Run.
            var chunk = SwarmChunk.BuildChunkFromHashAndData(hash, spanData, bmt);
            
            // Assert.
            var cac = Assert.IsType<SwarmCac>(chunk);
            Assert.Equal(hash, cac.Hash);
        }
        
        [Fact]
        public void BuildChunkFromHashAndDataBuildsSocOnMatchingAddress()
        {
            // Setup.
            var bmt = new SwarmChunkBmt();
            var soc = BuildSignedSoc(bmt);
            
            // Run.
            var chunk = SwarmChunk.BuildChunkFromHashAndData(soc.Hash, soc.GetFullPayloadToByteArray(), bmt);
            
            // Assert.
            var rebuilt = Assert.IsType<SwarmSoc>(chunk);
            Assert.Equal(soc.Hash, rebuilt.Hash);
            Assert.Equal(soc.Owner, rebuilt.Owner);
            Assert.Equal(soc.Identifier, rebuilt.Identifier);
        }
        
        [Fact]
        public void BuildChunkFromHashAndDataRejectsDataOfAnotherAddress()
        {
            // Setup.
            var bmt = new SwarmChunkBmt();
            var soc = BuildSignedSoc(bmt);
            var anotherAddress = soc.InnerChunk.Hash;
            
            // Run & Assert.
            var thrown = Assert.Throws<SwarmChunkVerificationException>(
                () => SwarmChunk.BuildChunkFromHashAndData(anotherAddress, soc.GetFullPayloadToByteArray(), bmt));
            Assert.Equal(anotherAddress, thrown.Hash);
        }
        
        [Fact]
        public void BuildChunkFromHashAndDataRejectsDataThatIsNoChunk()
        {
            // Setup.
            // Bytes that hash to another address and don't carry a recoverable signature.
            var bmt = new SwarmChunkBmt();
            var address = BuildInnerChunk(bmt).Hash;
            var data = Enumerable.Range(0, 200).Select(i => (byte)i).ToArray();
            
            // Run.
            var thrown = Assert.Throws<SwarmChunkVerificationException>(
                () => SwarmChunk.BuildChunkFromHashAndData(address, data, bmt));
            
            // Assert.
            Assert.Equal(address, thrown.Hash);
            Assert.IsAssignableFrom<ArgumentException>(thrown.InnerException);
        }
        
        [Fact]
        public void BuildChunkFromHashAndDataRejectsShortDataOfAnotherHash()
        {
            // Setup.
            // A content addressed chunk read for another address: too short to be a single owner chunk.
            var bmt = new SwarmChunkBmt();
            var spanData = BuildInnerChunk(bmt).SpanData;
            var anotherAddress = SwarmHash.Zero;
            
            // Run.
            var thrown = Assert.Throws<SwarmChunkVerificationException>(
                () => SwarmChunk.BuildChunkFromHashAndData(anotherAddress, spanData, bmt));
            
            // Assert.
            Assert.Equal(anotherAddress, thrown.Hash);
        }
        
        // Helpers.
        private static SwarmCac BuildInnerChunk(SwarmChunkBmt bmt)
        {
            var data = Enumerable.Range(0, 42).Select(i => (byte)i).ToArray();
            var spanData = SwarmCac.BuildFromData(SwarmHash.Zero, data).SpanData;
            return new SwarmCac(bmt.Hash(spanData), spanData);
        }
        
        private static SwarmSoc BuildSignedSoc(SwarmChunkBmt bmt)
        {
            var signer = new PrivateKeySigner(new EthPrivateKey(PrivateKeyHex));
            var identifier = Enumerable.Range(0, SwarmSocIdentifier.IdentifierSize).Select(i => (byte)i).ToArray();
            var soc = new SwarmSoc(identifier, signer.PublicAddress, BuildInnerChunk(bmt));
            soc.Sign(signer, new Hasher());
            return soc;
        }
    }
}
