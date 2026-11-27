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
using Etherna.SwarmSdk.Wasm.Postage;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;

namespace Etherna.SwarmSdk.Wasm
{
    /// <summary>
    /// Client side postage stamping, exported to JavaScript as <c>Etherna.SwarmSdk.Wasm.StamperExports</c>.
    /// A stamper signs the chunks of a postage batch with the key of its owner, and counts the chunks in each
    /// bucket of the batch, so that a chunk can be uploaded with its stamp instead of being stamped by the
    /// node. JavaScript holds a stamper by the number <see cref="CreateStamper"/> returns, and passes it to the
    /// exports that chunk or upload a content.
    /// </summary>
    /// <remarks>
    /// The bucket counters are the state of a batch: a stamper that restarts from empty counters reuses the
    /// slots of its earlier chunks, which overwrites them on the network. Save them with
    /// <see cref="GetStamperBuckets"/> and give them back to <see cref="CreateStamper"/>. A stamper reissues
    /// the same slot to a chunk it already stamped, only as long as it lives.
    /// </remarks>
    internal static partial class StamperExports
    {
        // Fields.
        private static readonly Dictionary<int, (PostageStamper Stamper, PostageBuckets Buckets)> stampers = [];
        private static int lastStamperId;

        // Methods.
        /// <summary>
        /// Create a stamper for a postage batch, and return its number.
        /// </summary>
        /// <param name="batchId">The hex id of the batch.</param>
        /// <param name="batchDepth">The depth of the batch.</param>
        /// <param name="isImmutable">True if the batch is immutable: a full bucket fails the stamping, instead
        /// of overwriting its oldest slot.</param>
        /// <param name="privateKey">The hex private key of the owner of the batch.</param>
        /// <param name="buckets">The bucket counters of the batch, as <see cref="GetStamperBuckets"/> returns
        /// them, or null for a batch with no chunk.</param>
        [JSExport]
        internal static int CreateStamper(
            string batchId,
            int batchDepth,
            bool isImmutable,
            string privateKey,
            byte[]? buckets)
        {
            var key = new EthPrivateKey(privateKey);
            var postageBatch = new PostageBatch(
                new PostageBatchId(batchId),
                amount: null,
                blockNumber: 0,
                depth: batchDepth,
                exists: true,
                isImmutable: isImmutable,
                isUsable: true,
                label: null,
                ttl: TimeSpan.Zero,
                utilization: 0);
            var postageBuckets = new PostageBuckets(buckets is null ? null : ReadBuckets(buckets));
            var stamper = new PostageStamper(
                new PrivateKeySigner(key),
                new PostageStampIssuer(postageBatch, key.Address, postageBuckets),
                new MemoryStampStore());

            var stamperId = ++lastStamperId;
            stampers.Add(stamperId, (stamper, postageBuckets));
            return stamperId;
        }

        /// <summary>
        /// Release a stamper.
        /// </summary>
        [JSExport]
        internal static void DisposeStamper(int stamper)
        {
            if (!stampers.Remove(stamper, out var entry))
                throw BuildUnknownStamperException(stamper);
            entry.Buckets.Dispose();
        }

        /// <summary>
        /// The bucket counters of the batch of a stamper: 65536 unsigned 32 bit little endian integers, the
        /// chunks stamped in each bucket.
        /// </summary>
        [JSExport]
        internal static byte[] GetStamperBuckets(int stamper)
        {
            var buckets = GetStamperEntry(stamper).Buckets.GetBuckets();
            var bytes = new byte[buckets.Length * sizeof(uint)];
            for (var i = 0; i < buckets.Length; i++)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint)), buckets[i]);
            return bytes;
        }

        /// <summary>
        /// The depth a batch needs to hold the chunks a stamper stamped, from its fullest bucket.
        /// </summary>
        [JSExport]
        internal static int GetStamperRequiredBatchDepth(int stamper) =>
            GetStamperEntry(stamper).Buckets.RequiredPostageBatchDepth;

        /// <summary>
        /// Stamp a chunk, and return the stamp in the 113 bytes form a bee node takes in its
        /// <c>swarm-postage-stamp</c> header: batch id, bucket index, timestamp and signature.
        /// </summary>
        /// <param name="stamper">The stamper.</param>
        /// <param name="chunkAddress">The hex address of the chunk.</param>
        [JSExport]
        internal static byte[] StampChunk(int stamper, string chunkAddress) =>
            GetStamperEntry(stamper).Stamper.Stamp(new SwarmHash(chunkAddress)).ToByteArray();

        // Internal methods.
        /// <summary>
        /// The stamper of a write: the stamper with the given number, whose stamps are kept for the upload of
        /// the chunks, or, without a number, a stamper that only counts buckets, for the compaction.
        /// </summary>
        internal static RecordingPostageStamper BuildWriteStamper(int? stamper) =>
            stamper is null
                ? new RecordingPostageStamper(
                    new PostageStamper(
                        new FakeSigner(),
                        new PostageStampIssuer(PostageBatch.MaxDepthInstance),
                        new MemoryStampStore()),
                    keepStamps: false)
                : new RecordingPostageStamper(GetStamperEntry(stamper.Value).Stamper, keepStamps: true);

        internal static PostageStamper GetStamper(int stamper) => GetStamperEntry(stamper).Stamper;

        // Helpers.
        private static ArgumentException BuildUnknownStamperException(int stamper) =>
            new($"No stamper {stamper}", nameof(stamper));

        private static (PostageStamper Stamper, PostageBuckets Buckets) GetStamperEntry(int stamper) =>
            stampers.TryGetValue(stamper, out var entry) ? entry : throw BuildUnknownStamperException(stamper);

        private static uint[] ReadBuckets(byte[] bytes)
        {
            if (bytes.Length != PostageBuckets.BucketsSize * sizeof(uint))
                throw new ArgumentOutOfRangeException(
                    nameof(bytes),
                    $"Buckets must be {PostageBuckets.BucketsSize * sizeof(uint)} bytes");

            var buckets = new uint[PostageBuckets.BucketsSize];
            for (var i = 0; i < buckets.Length; i++)
                buckets[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint)));
            return buckets;
        }
    }
}
