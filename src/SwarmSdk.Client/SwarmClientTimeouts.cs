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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Etherna.SwarmSdk
{
    /// <summary>
    /// The bound of each <see cref="SwarmClient"/> call, by the class of operation the node performs for it.
    /// A call that the node does not complete within its bound throws
    /// <see cref="Exceptions.SwarmClientTimeoutException"/>. Each bound is positive, or
    /// <see cref="Timeout.InfiniteTimeSpan"/> for no bound.
    /// </summary>
    public sealed class SwarmClientTimeouts
    {
        // Fields.
        private readonly TimeSpan chainRead = TimeSpan.FromSeconds(30);
        private readonly TimeSpan chunkPush = TimeSpan.FromMinutes(4);
        private readonly TimeSpan chunkRetrieval = TimeSpan.FromMinutes(2);
        private readonly TimeSpan contentTransfer = TimeSpan.FromMinutes(10);
        private readonly TimeSpan listing = TimeSpan.FromSeconds(60);
        private readonly TimeSpan local = TimeSpan.FromSeconds(5);
        private readonly TimeSpan peerDial = TimeSpan.FromSeconds(60);
        private readonly TimeSpan transactionSend = TimeSpan.FromSeconds(60);
        private readonly TimeSpan transactionWait = TimeSpan.FromMinutes(10);

        // Properties.
        /// <summary>
        /// A read the node answers through its chain RPC endpoint: chain state, chequebook balance and cashout status,
        /// wallet balances, stake. Default 30 seconds.
        /// </summary>
        public TimeSpan ChainRead
        {
            get => chainRead;
            init => chainRead = Validate(value, nameof(ChainRead));
        }

        /// <summary>
        /// A push of a few chunks to the network, waiting for their receipts: a chunk, a single owner chunk, a feed
        /// manifest, a grantee list, a pss message. Default 4 minutes.
        /// </summary>
        public TimeSpan ChunkPush
        {
            get => chunkPush;
            init => chunkPush = Validate(value, nameof(ChunkPush));
        }

        /// <summary>
        /// A retrieval from the network, up to the first byte of the answer: a chunk, a single owner chunk, a feed
        /// lookup, the bytes or the file at a reference, a grantee list. Default 2 minutes.
        /// </summary>
        public TimeSpan ChunkRetrieval
        {
            get => chunkRetrieval;
            init => chunkRetrieval = Validate(value, nameof(ChunkRetrieval));
        }

        /// <summary>
        /// An operation whose duration grows with the content: the upload of bytes, a file or a directory, a bulk
        /// chunk upload, a pin, an unpin, a pin check, a stewardship check or reupload, a reserve commitment.
        /// Default 10 minutes.
        /// </summary>
        public TimeSpan ContentTransfer
        {
            get => contentTransfer;
            init => contentTransfer = Validate(value, nameof(ContentTransfer));
        }

        /// <summary>
        /// A listing that can be as large as the network: the valid postage batches, the owned postage batches,
        /// the pins, the peers status, the neighborhoods status. Default 60 seconds.
        /// </summary>
        public TimeSpan Listing
        {
            get => listing;
            init => listing = Validate(value, nameof(Listing));
        }

        /// <summary>
        /// A read or write the node answers from its own state: health, readiness, node info and addresses, a
        /// postage batch, a pin, a chunk existence check, tags, cheques, balances, settlements, peers, topology,
        /// transactions, loggers, the welcome message, and the handshake of a WebSocket. Default 5 seconds.
        /// </summary>
        public TimeSpan Local
        {
            get => local;
            init => local = Validate(value, nameof(Local));
        }

        /// <summary>
        /// A call the node completes with one peer over the network: a connection, a ping. libp2p bounds the dial at
        /// 15 seconds and bee's handshake at 15 seconds more, so the node reports its own failure within about 30
        /// seconds; the bound leaves it room. Default 60 seconds.
        /// </summary>
        public TimeSpan PeerDial
        {
            get => peerDial;
            init => peerDial = Validate(value, nameof(PeerDial));
        }

        /// <summary>
        /// A transaction the node answers once broadcast, with its hash: a cheque cashout, a chequebook deposit or
        /// withdraw, a wallet withdraw, a transaction rebroadcast or cancel. Default 60 seconds.
        /// </summary>
        public TimeSpan TransactionSend
        {
            get => transactionSend;
            init => transactionSend = Validate(value, nameof(TransactionSend));
        }

        /// <summary>
        /// A transaction the node answers once mined: a postage batch purchase, top-up or dilution, a stake
        /// deposit or withdraw. Default 10 minutes.
        /// </summary>
        public TimeSpan TransactionWait
        {
            get => transactionWait;
            init => transactionWait = Validate(value, nameof(TransactionWait));
        }

        // Internals.
        /// <summary>Each bound, by the name of its class.</summary>
        internal IEnumerable<KeyValuePair<string, TimeSpan>> Bounds =>
        [
            new(nameof(ChainRead), ChainRead),
            new(nameof(ChunkPush), ChunkPush),
            new(nameof(ChunkRetrieval), ChunkRetrieval),
            new(nameof(ContentTransfer), ContentTransfer),
            new(nameof(Listing), Listing),
            new(nameof(Local), Local),
            new(nameof(PeerDial), PeerDial),
            new(nameof(TransactionSend), TransactionSend),
            new(nameof(TransactionWait), TransactionWait)
        ];

        /// <summary>The longest bound: <see cref="Timeout.InfiniteTimeSpan"/> when any class has no bound.</summary>
        internal TimeSpan Longest =>
            Bounds.Any(b => b.Value == Timeout.InfiniteTimeSpan)
                ? Timeout.InfiniteTimeSpan
                : Bounds.Max(b => b.Value);

        // Helpers.
        private static TimeSpan Validate(TimeSpan value, string name) =>
            value > TimeSpan.Zero || value == Timeout.InfiniteTimeSpan
                ? value
                : throw new ArgumentOutOfRangeException(name, value, "A timeout is positive, or Timeout.InfiniteTimeSpan.");
    }
}
