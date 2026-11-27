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

using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Collections.Concurrent;

namespace Etherna.SwarmSdk.Wasm.Postage
{
    /// <summary>
    /// A postage stamper keeping the stamps it issues during one write, by chunk address. The hasher pipeline
    /// stamps a chunk right before it hands the chunk to its chunk store, and drops the stamp: a store that
    /// uploads the chunk takes the stamp from here.
    /// </summary>
    /// <param name="stamper">The stamper issuing the stamps.</param>
    /// <param name="keepStamps">False when the stamper only counts buckets, with a fake signer: its stamps are
    /// not valid for an upload, and none is kept.</param>
    internal sealed class RecordingPostageStamper(IPostageStamper stamper, bool keepStamps) : IPostageStamper
    {
        // Fields.
        //a content can repeat a chunk: every stamp of an address is valid, the last one wins
        private readonly ConcurrentDictionary<SwarmHash, PostageStamp> stamps = new();

        // Properties.
        public ISigner Signer => stamper.Signer;
        public IPostageStampIssuer StampIssuer => stamper.StampIssuer;
        public IStampStore StampStore => stamper.StampStore;

        // Methods.
        public PostageStamp? GetStamp(SwarmHash hash) =>
            stamps.TryGetValue(hash, out var stamp) ? stamp : null;

        public PostageStamp Stamp(SwarmHash hash)
        {
            var stamp = stamper.Stamp(hash);
            if (keepStamps)
                stamps[hash] = stamp;
            return stamp;
        }
    }
}
