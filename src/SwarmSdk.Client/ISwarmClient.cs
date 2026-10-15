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
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk
{
    public interface ISwarmClient
    {
        // Properties.
        public SwarmClients ApiCompatibility { get; }
        public bool IsDryMode { get; }
        public Uri NodeUrl { get; }

        // Methods.
        /// <summary>Get all accounting associated values with all known peers.</summary>
        /// <returns>Own accounting associated values with all known peers</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<Dictionary<string, Account>> AccountingAsync(CancellationToken cancellationToken = default);

        /// <summary>Buy a new postage batch.</summary>
        /// <param name="amount">Amount of BZZ added that the postage batch will have.</param>
        /// <param name="depth">Batch depth which specifies how many chunks can be signed with the batch. It is a logarithm. Must be higher than default bucket depth (16).</param>
        /// <param name="label">An optional label for this batch.</param>
        /// <param name="immutable">Determines if the postage batch is immutable. An immutable batch cannot be diluted (have its depth increased).</param>
        /// <param name="gasLimit">Gas limit for transaction.</param>
        /// <param name="gasPrice">Gas price for transaction.</param>
        /// <returns>Returns the newly created postage batch ID</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<(PostageBatchId BatchId, EthTxHash TxHash)> BuyPostageBatchAsync(
            BzzValue amount,
            int depth,
            string? label = null,
            bool? immutable = null,
            ulong? gasLimit = null,
            XDaiValue? gasPrice = null,
            CancellationToken cancellationToken = default);

        /// <summary>Cashout the last cheque for the peer.</summary>
        /// <param name="peerId">Swarm address of peer.</param>
        /// <param name="gasPrice">Gas price for transaction.</param>
        /// <param name="gasLimit">Gas limit for transaction.</param>
        /// <returns>Hash of the transaction</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<EthTxHash> CashoutChequeForPeerAsync(
            string peerId,
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default);

        /// <summary>Validate pinned chunks integrity.</summary>
        /// <param name="reference">Optional reference to check; if not provided, all pinned references are checked.</param>
        /// <returns>Result of the pinned chunks integrity check</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<CheckPinsResult> CheckPinsAsync(
            SwarmReference? reference,
            CancellationToken cancellationToken = default);

        /// <summary>Upload a collection of chunks in a single bulk request.</summary>
        /// <param name="chunks">Chunks to upload.</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="maxUploadAttempts">Maximum number of attempts per chunk push (initial try plus retries). 1 disables retrying. Retrying is safe because re-sending already-stamped chunks is idempotent.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task ChunksBulkUploadAsync(
            SwarmChunk[] chunks,
            PostageBatchId batchId,
            int maxUploadAttempts = 1,
            CancellationToken cancellationToken = default);

        /// <summary>Connect to address.</summary>
        /// <param name="peerAddress">Underlay address of peer.</param>
        /// <returns>Returns overlay address of connected peer</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SwarmOverlayAddress> ConnectToPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default);

        /// <summary>Pin the root hash with the given reference.</summary>
        /// <param name="reference">Swarm content reference.</param>
        /// <param name="redundancyLevel">The redundancy level of the content, used to traverse redundant data.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<bool> CreatePinAsync(
            SwarmReference reference,
            RedundancyLevel? redundancyLevel = null,
            CancellationToken cancellationToken = default);

        /// <summary>Create a new tag.</summary>
        /// <param name="hash">Swarm hash to associate with the tag.</param>
        /// <returns>New tag info</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<TagInfo> CreateTagAsync(
            SwarmHash hash,
            CancellationToken cancellationToken = default);

        /// <summary>Get a snapshot of local storage debug info.</summary>
        /// <returns>Local storage debug info</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<DebugChunkStore> DebugChunkStoreAsync(CancellationToken cancellationToken = default);

        /// <summary>Remove peer.</summary>
        /// <param name="peerAddress">Swarm address of peer.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task DeletePeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default);

        /// <summary>Unpin the root hash with the given reference.</summary>
        /// <param name="reference">Swarm content reference.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task DeletePinAsync(
            SwarmReference reference,
            CancellationToken cancellationToken = default);

        /// <summary>Delete tag information by ID.</summary>
        /// <param name="id">Tag ID.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task DeleteTagAsync(
            TagId id,
            CancellationToken cancellationToken = default);

        /// <summary>Cancel existing transaction.</summary>
        /// <param name="txHash">Hash of the transaction.</param>
        /// <param name="gasPrice">Gas price for transaction.</param>
        /// <returns>Hash of the transaction</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<EthTxHash> DeleteTransactionAsync(
            EthTxHash txHash,
            XDaiValue? gasPrice = null,
            CancellationToken cancellationToken = default);

        /// <summary>Deposit tokens from overlay address into chequebook.</summary>
        /// <param name="amount">Amount of tokens to deposit.</param>
        /// <param name="gasPrice">Gas price for transaction.</param>
        /// <returns>Transaction hash of the deposit transaction</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<EthTxHash> DepositIntoChequebookAsync(
            BzzValue amount,
            XDaiValue? gasPrice = null,
            CancellationToken cancellationToken = default);

        /// <summary>Dilute an existing postage batch.</summary>
        /// <param name="batchId">Batch ID to dilute.</param>
        /// <param name="depth">The new batch depth, which must be greater than the current depth.</param>
        /// <param name="gasPrice">Gas price for transaction.</param>
        /// <param name="gasLimit">Gas limit for transaction.</param>
        /// <returns>Returns the tx hash updating the postage batch</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<EthTxHash> DilutePostageBatchAsync(
            PostageBatchId batchId,
            int depth,
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default);

        /// <summary>Create a postage stamp for a chunk.</summary>
        /// <param name="address">Swarm address of the chunk to stamp.</param>
        /// <param name="batchId">ID of the postage batch to use.</param>
        /// <returns>The created envelope</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<EnvelopeResponse> EnvelopeAsync(
            string address,
            PostageBatchId batchId,
            CancellationToken cancellationToken = default);

        /// <summary>Get the balances with all known peers including prepaid services.</summary>
        /// <returns>Own balances with all known peers</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<PeerBalance[]> GetAllBalancesAsync(CancellationToken cancellationToken = default);

        /// <summary>Get last cheques for all peers.</summary>
        /// <returns>Last cheques</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<ChequebookCheque[]> GetAllChequebookChequesAsync(CancellationToken cancellationToken = default);

        /// <summary>Get the past due consumption balances with all known peers.</summary>
        /// <returns>Own past due consumption balances with all known peers</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<IEnumerable<PeerBalance>> GetAllConsumedBalancesAsync(CancellationToken cancellationToken = default);

        /// <summary>Get a list of peers.</summary>
        /// <returns>Returns overlay addresses of connected peers</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<(string Address, bool FullNode)[]> GetAllPeerAddressesAsync(CancellationToken cancellationToken = default);

        /// <summary>Get the list of pinned root hash references.</summary>
        /// <returns>List of pinned references</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SwarmReference[]> GetAllPinsAsync(CancellationToken cancellationToken = default);

        /// <summary>Get settlements with all known peers and total amount sent or received.</summary>
        /// <returns>Settlements with all known peers and total amount sent or received</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<Settlement> GetAllSettlementsAsync(CancellationToken cancellationToken = default);

        /// <summary>Get time based settlements with all known peers and total amount sent or received.</summary>
        /// <returns>Time based settlements with all known peers and total amount sent or received</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<Settlement> GetAllTimeSettlementsAsync(CancellationToken cancellationToken = default);

        /// <summary>Get the balances with a specific peer including prepaid services.</summary>
        /// <param name="peerAddress">Swarm address of peer.</param>
        /// <returns>Balance with the specific peer</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<PeerBalance> GetBalanceWithPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default);

        /// <summary>Get referenced data.</summary>
        /// <param name="reference">Swarm content reference.</param>
        /// <param name="cache">Determines if the download data should be cached on the node. By default the download will be cached.</param>
        /// <param name="redundancyLevel">The redundancy level of the content, used to determine the retrieve strategy on redundant data.</param>
        /// <param name="redundancyStrategy">Specify the retrieve strategy on redundant data. The numbers stand for NONE, DATA, PROX and RACE, respectively. Strategy NONE means no prefetching takes place. Strategy DATA means only data chunks are prefetched. Strategy PROX means only chunks that are close to the node are prefetched. Strategy RACE means all chunks are prefetched: n data chunks and k parity chunks. The first n chunks to arrive are used to reconstruct the file. Multiple strategies can be used in a fallback cascade if the swarm redundancy fallback mode is set to true. The default strategy is NONE, DATA, falling back to PROX, falling back to RACE.</param>
        /// <param name="redundancyFallbackMode">Specify if the retrieve strategies (chunk prefetching on redundant data) are used in a fallback cascade. The default is true.</param>
        /// <param name="chunkRetrievalTimeout">Specify the timeout for chunk retrieval. The default is 30 seconds.</param>
        /// <param name="lookaheadBufferSize">Override the lookahead buffer size used during retrieval, in bytes. When unset the node picks 8x or 16x the io.Copy default buffer (32 kB) depending on file size.</param>
        /// <param name="actTimestamp">ACT history Unix timestamp.</param>
        /// <param name="actPublisher">ACT content publisher's public key.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <returns>Retrieved content specified by reference</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<Stream> GetBytesAsync(
            SwarmReference reference,
            bool? cache = null,
            RedundancyLevel? redundancyLevel = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyFallbackMode = null,
            string? chunkRetrievalTimeout = null,
            int? lookaheadBufferSize = null,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default);

        /// <summary>Request the headers containing the content type and length for the reference.</summary>
        /// <param name="reference">Swarm content reference.</param>
        /// <param name="actTimestamp">ACT history Unix timestamp.</param>
        /// <param name="actPublisher">ACT content publisher's public key.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <param name="cache">Determines if the download data should be cached on the node. By default the download will be cached.</param>
        /// <param name="redundancyLevel">The redundancy level of the content, used to determine the retrieve strategy on redundant data.</param>
        /// <param name="redundancyStrategy">Specify the retrieve strategy on redundant data.</param>
        /// <param name="redundancyFallbackMode">Specify if the retrieve strategies (chunk prefetching on redundant data) are used in a fallback cascade. The default is true.</param>
        /// <param name="chunkRetrievalTimeout">Specify the timeout for chunk retrieval. The default is 30 seconds.</param>
        /// <returns>Content headers</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<HttpContentHeaders?> GetBytesHeadersAsync(
            SwarmReference reference,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            bool? cache = null,
            RedundancyLevel? redundancyLevel = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyFallbackMode = null,
            string? chunkRetrievalTimeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>Get a list of blocklisted peers.</summary>
        /// <returns>Returns overlay addresses of blocklisted peers</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<BlockedPeer[]> GetBlocklistedPeerAddressesAsync(CancellationToken cancellationToken = default);

        /// <summary>Get chain state.</summary>
        /// <returns>Chain state</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<ChainState> GetChainStateAsync(CancellationToken cancellationToken = default);

        /// <summary>Get the address of the chequebook contract used.</summary>
        /// <returns>Ethereum address of chequebook contract</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<EthAddress> GetChequebookAddressAsync(CancellationToken cancellationToken = default);

        /// <summary>Get the balance of the chequebook.</summary>
        /// <returns>Balance of the chequebook</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<ChequebookBalance> GetChequebookBalanceAsync(CancellationToken cancellationToken = default);

        /// <summary>Get last cashout action for the peer.</summary>
        /// <param name="peerAddress">Swarm address of peer.</param>
        /// <returns>Cashout status</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<ChequebookCashout> GetChequebookCashoutForPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default);

        /// <summary>Get last cheques for the peer.</summary>
        /// <param name="peerId">Swarm address of peer.</param>
        /// <returns>Last cheques</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<ChequebookCheque> GetChequebookChequeForPeerAsync(
            string peerId,
            CancellationToken cancellationToken = default);

        /// <summary>Get a chunk.</summary>
        /// <param name="hash">Swarm hash of the chunk.</param>
        /// <param name="swarmChunkBmt">BMT used to compute and verify the chunk content address.</param>
        /// <param name="cache">Determines if the download data should be cached on the node. By default the download will be cached.</param>
        /// <param name="actTimestamp">ACT history Unix timestamp.</param>
        /// <param name="actPublisher">ACT content publisher's public key.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <returns>Retrieved chunk content</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SwarmChunk> GetChunkAsync(
            SwarmHash hash,
            SwarmChunkBmt swarmChunkBmt,
            bool? cache = null,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default);

        /// <summary>Get a chunk as a stream.</summary>
        /// <param name="hash">Swarm hash of the chunk.</param>
        /// <param name="cache">Determines if the download data should be cached on the node. By default the download will be cached.</param>
        /// <param name="actTimestamp">ACT history Unix timestamp.</param>
        /// <param name="actPublisher">ACT content publisher's public key.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <returns>Retrieved chunk content</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<Stream> GetChunkStreamAsync(
            SwarmHash hash,
            bool? cache = null,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default);

        /// <summary>Stream chunks for upload over a WebSocket connection.</summary>
        /// <remarks>Each uploaded chunk receives a binary acknowledgment (`0`). Chunks are sent as binary messages. When a tag is specified, chunks are stored locally and uploaded to the network after the stream closes; without a tag, chunks are directly uploaded to the network as they arrive.</remarks>
        /// <param name="tagHeader">Associate upload with an existing Tag UID (sent as a request header).</param>
        /// <param name="tagQuery">Associate upload with an existing Tag UID (use when the WebSocket client cannot set custom headers).</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with. Optional when chunks include pre-signed postage stamps.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task GetChunksStreamWebSocketAsync(
            ulong? tagHeader = null,
            ulong? tagQuery = null,
            PostageBatchId? batchId = null,
            CancellationToken cancellationToken = default);

        /// <summary>Upload stream of chunks.</summary>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="tagId">Associate upload with an existing Tag UID.</param>
        /// <returns>Returns a Websocket connection on which stream of chunks can be uploaded. Each chunk sent is acknowledged using a binary response `0` which serves as confirmation of upload of single chunk. Chunks should be packaged as binary messages for uploading.</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<IChunkWebSocketUploader> GetChunkUploaderWebSocketAsync(
            PostageBatchId batchId,
            TagId? tagId = null,
            CancellationToken cancellationToken = default);

        /// <summary>Get the past due consumption balance with a specific peer.</summary>
        /// <param name="peerAddress">Swarm address of peer.</param>
        /// <returns>Past-due consumption balance with the specific peer</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<PeerBalance> GetConsumedBalanceWithPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default);

        /// <summary>Get file or index document from a collection of files.</summary>
        /// <param name="address">Swarm address of content.</param>
        /// <param name="cache">Determines if the download data should be cached on the node. By default the download will be cached.</param>
        /// <param name="redundancyLevel">The redundancy level of the content, used to determine the retrieve strategy on redundant data.</param>
        /// <param name="redundancyStrategy">Specify the retrieve strategy on redundant data. The numbers stand for NONE, DATA, PROX and RACE, respectively. Strategy NONE means no prefetching takes place. Strategy DATA means only data chunks are prefetched. Strategy PROX means only chunks that are close to the node are prefetched. Strategy RACE means all chunks are prefetched: n data chunks and k parity chunks. The first n chunks to arrive are used to reconstruct the file. Multiple strategies can be used in a fallback cascade if the swarm redundancy fallback mode is set to true. The default strategy is NONE, DATA, falling back to PROX, falling back to RACE.</param>
        /// <param name="redundancyFallbackMode">Specify if the retrieve strategies (chunk prefetching on redundant data) are used in a fallback cascade. The default is true.</param>
        /// <param name="chunkRetrievalTimeout">Specify the timeout for chunk retrieval. The default is 30 seconds.</param>
        /// <param name="lookaheadBufferSize">Override the lookahead buffer size used during retrieval, in bytes. When unset the node picks 8x or 16x the io.Copy default buffer (32 kB) depending on file size.</param>
        /// <param name="actTimestamp">ACT history Unix timestamp.</param>
        /// <param name="actPublisher">ACT content publisher's public key.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <returns>Retrieved file content</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<FileResponse> GetFileAsync(
            SwarmAddress address,
            bool? cache = null,
            RedundancyLevel? redundancyLevel = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyFallbackMode = null,
            string? chunkRetrievalTimeout = null,
            int? lookaheadBufferSize = null,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default);

        /// <summary>Get all globally available batches that were purchased by all nodes.</summary>
        /// <param name="batchId">Optional postage batch ID used to filter the results.</param>
        /// <returns>Returns a dictionary with owner as keys, and enumerable of currently valid owned postage batches as values.</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<(PostageBatch PostageBatch, EthAddress Owner)[]> GetGlobalValidPostageBatchesAsync(
            PostageBatchId? batchId = null,
            CancellationToken cancellationToken = default);

        /// <summary>Get health of node.</summary>
        /// <returns>Health state of node</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<Health> GetHealthAsync(CancellationToken cancellationToken = default);

        /// <summary>Get the current neighborhoods status of this node.</summary>
        /// <returns>Returns the neighborhoods status of this node</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<NeighborhoodStatus[]> GetNeighborhoodsStatus(
            CancellationToken cancellationToken = default);

        /// <summary>Get overlay and underlay addresses of the node.</summary>
        /// <returns>Own node underlay and overlay addresses</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SwarmNodeAddresses> GetNodeAddressesAsync(CancellationToken cancellationToken = default);

        /// <summary>Get information about the node.</summary>
        /// <returns>Information about the node</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<NodeInfo> GetNodeInfoAsync(CancellationToken cancellationToken = default);

        /// <summary>Get all owned postage batches by this node.</summary>
        /// <returns>List of all owned postage batches</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<PostageBatch[]> GetOwnedPostageBatchesAsync(CancellationToken cancellationToken = default);

        /// <summary>Get list of pending transactions.</summary>
        /// <returns>List of pending transactions</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<GnosisChainTx[]> GetPendingTransactionsAsync(CancellationToken cancellationToken = default);

        /// <summary>Get pinning status of the root hash with the given reference.</summary>
        /// <param name="reference">Swarm content reference.</param>
        /// <returns>Pinning status of the reference</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<PinStatus> GetPinStatusAsync(
            SwarmReference reference,
            CancellationToken cancellationToken = default);

        /// <summary>Get an individual postage batch status.</summary>
        /// <param name="batchId">Postage batch ID.</param>
        /// <returns>Returns an individual postage batch state</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<PostageBatch> GetPostageBatchAsync(
            PostageBatchId batchId,
            CancellationToken cancellationToken = default);

        /// <summary>Get extended bucket data of a batch.</summary>
        /// <param name="batchId">Postage batch ID.</param>
        /// <returns>Returns extended bucket data of the provided batch ID</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<PostageBucketsStatus> GetPostageBatchBucketsAsync(
            PostageBatchId batchId,
            CancellationToken cancellationToken = default);

        /// <summary>Check if the node is ready to accept traffic.</summary>
        /// <returns>Returns true if the node is ready</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<bool> GetReadinessAsync(CancellationToken cancellationToken = default);

        /// <summary>Get reserve commitment hash with sample proofs.</summary>
        /// <param name="depth">The storage depth.</param>
        /// <param name="anchor1">The first anchor.</param>
        /// <param name="anchor2">The second anchor.</param>
        /// <returns>Reserve sample response</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<ReserveCommitment> GetReserveCommitmentAsync(
            int depth,
            string anchor1,
            string anchor2,
            CancellationToken cancellationToken = default);

        /// <summary>Get reserve state.</summary>
        /// <returns>Reserve state</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<ReserveState> GetReserveStateAsync(CancellationToken cancellationToken = default);

        /// <summary>Get amount of sent and received from settlements with a peer.</summary>
        /// <param name="peerAddress">Swarm address of peer.</param>
        /// <returns>Amount of sent or received from settlements with a peer</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SettlementData> GetSettlementsWithPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default);

        /// <summary>Download a Single Owner Chunk and parse it into a <see cref="SwarmSoc"/>.</summary>
        /// <param name="owner">Ethereum address of the SOC owner.</param>
        /// <param name="identifier">Unique identifier of the SOC.</param>
        /// <param name="cache">Determines if the download data should be cached on the node. By default the download will be cached.</param>
        /// <param name="actTimestamp">ACT timestamp.</param>
        /// <param name="actPublisher">ACT publisher public key.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <returns>The downloaded single owner chunk</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SwarmSoc> GetSocAsync(
            EthAddress owner,
            SwarmSocIdentifier identifier,
            bool? cache = null,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default);

        /// <summary>Retrieve Single Owner Chunk data.</summary>
        /// <param name="owner">Ethereum address of the Owner of the SOC.</param>
        /// <param name="id">Unique identifier for the chunk data.</param>
        /// <param name="onlyRootChunk">Returns only the root chunk of the content.</param>
        /// <param name="cache">Determines if the download data should be cached on the node. By default the download will be cached.</param>
        /// <param name="redundancyStrategy">Specify the retrieve strategy on redundant data.</param>
        /// <param name="redundancyFallbackMode">Specify if the retrieve strategies (chunk prefetching on redundant data) are used in a fallback cascade. The default is true.</param>
        /// <param name="chunkRetrievalTimeout">Specify the timeout for chunk retrieval. The default is 30 seconds.</param>
        /// <returns>Retrieved single owner chunk content</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<FileResponse> GetSocDataAsync(
            EthAddress owner,
            string id,
            bool? onlyRootChunk = null,
            bool? cache = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyFallbackMode = null,
            string? chunkRetrievalTimeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>Get the swarm topology of the bee node.</summary>
        /// <returns>Swarm topology of the bee node</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<Topology> GetSwarmTopologyAsync(CancellationToken cancellationToken = default);

        /// <summary>Get tag information by ID.</summary>
        /// <param name="id">Tag ID.</param>
        /// <returns>Tag info</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<TagInfo> GetTagInfoAsync(
            TagId id,
            CancellationToken cancellationToken = default);

        /// <summary>Get list of tags.</summary>
        /// <param name="offset">The number of items to skip before starting to collect the result set.</param>
        /// <param name="limit">The numbers of items to return.</param>
        /// <returns>List of tags</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<TagInfo[]> GetTagsListAsync(
            int? offset = null,
            int? limit = null,
            CancellationToken cancellationToken = default);

        /// <summary>Get information about a sent transaction.</summary>
        /// <param name="txHash">Hash of the transaction.</param>
        /// <returns>Info about the transaction</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<GnosisChainTx> GetTransactionInfoAsync(
            EthTxHash txHash,
            CancellationToken cancellationToken = default);

        /// <summary>Get wallet balance for BZZ and xDai.</summary>
        /// <returns>Wallet balance info</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<WalletBalances> GetWalletBalance(CancellationToken cancellationToken = default);

        /// <summary>Get configured P2P welcome message.</summary>
        /// <returns>Welcome message</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<string> GetWelcomeMessageAsync(CancellationToken cancellationToken = default);

        /// <summary>Get grantee list.</summary>
        /// <param name="reference">Grantee list reference.</param>
        /// <param name="redundancyLevel">The redundancy level of the grantee list, used to determine the retrieve strategy on redundant data.</param>
        /// <returns>Ok</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<ICollection<string>> GranteeGetAsync(
            string reference,
            RedundancyLevel? redundancyLevel = null,
            CancellationToken cancellationToken = default);

        /// <summary>Update grantee list.</summary>
        /// <remarks>Add or remove grantees from an existing grantee list.</remarks>
        /// <param name="reference">Grantee list reference.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="addList">List of grantee public keys to add.</param>
        /// <param name="revokeList">List of grantee public keys to revoke.</param>
        /// <param name="tagId">Associate upload with an existing Tag UID.</param>
        /// <param name="pin">Represents if the uploaded data should be also locally pinned on the node.</param>
        /// <param name="deferredUpload">Determines if the uploaded data should be sent to the network immediately or in a deferred fashion. By default the upload will be deferred.</param>
        /// <param name="redundancyLevel">Add redundancy to the grantee list being uploaded so that downloaders can download it with better UX. 0 value is default and does not add any redundancy.</param>
        /// <returns>Ok</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<GranteeResponse> GranteePatchAsync(
            string reference,
            string actHistoryAddress,
            PostageBatchId batchId,
            string[] addList,
            string[] revokeList,
            TagId? tagId = null,
            bool? pin = null,
            bool? deferredUpload = null,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            CancellationToken cancellationToken = default);

        /// <summary>Create grantee list.</summary>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="grantees">List of grantee public keys.</param>
        /// <param name="tagId">Associate upload with an existing Tag UID.</param>
        /// <param name="pin">Represents if the uploaded data should be also locally pinned on the node.</param>
        /// <param name="deferredUpload">Determines if the uploaded data should be sent to the network immediately or in a deferred fashion. By default the upload will be deferred.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <param name="redundancyLevel">Add redundancy to the grantee list being uploaded so that downloaders can download it with better UX. 0 value is default and does not add any redundancy.</param>
        /// <returns>Ok</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<GranteeResponse> GranteePostAsync(
            PostageBatchId batchId,
            string[] grantees,
            TagId? tagId = null,
            bool? pin = null,
            bool? deferredUpload = null,
            string? actHistoryAddress = null,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            CancellationToken cancellationToken = default);

        /// <summary>Check if chunk at address exists locally.</summary>
        /// <param name="hash">Swarm hash of chunk.</param>
        /// <param name="actTimestamp">ACT history Unix timestamp.</param>
        /// <param name="actPublisher">ACT content publisher's public key.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <returns>Chunk exists</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<bool> IsChunkExistingAsync(
            SwarmHash hash,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default);

        /// <summary>Check if content is retrievable.</summary>
        /// <param name="reference">Swarm content reference.</param>
        /// <param name="redundancyLevel">The redundancy level of the content, used to traverse redundant data.</param>
        /// <returns>Returns if the content is retrievable</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<bool> IsContentRetrievableAsync(
            SwarmReference reference,
            RedundancyLevel? redundancyLevel = null,
            CancellationToken cancellationToken = default);

        /// <summary>Get all available loggers.</summary>
        /// <returns>Returns an array of all available loggers, also represented in short form in a tree.</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<LogData> LoggersGetAsync(CancellationToken cancellationToken = default);

        /// <summary>Get all available loggers that match the specified expression.</summary>
        /// <param name="exp">Regular expression or a subsystem that matches the logger(s).</param>
        /// <returns>Returns an array of all available loggers that matches given expression, also represented in short form in a tree.</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<LogData> LoggersGetAsync(string exp, CancellationToken cancellationToken = default);

        /// <summary>Set logger(s) verbosity level.</summary>
        /// <param name="exp">Regular expression or a subsystem that matches the logger(s).</param>
        /// <param name="verbosity">Verbosity level to apply to the matching logger(s).</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task LoggersPutAsync(string exp, Verbosity verbosity, CancellationToken cancellationToken = default);

        /// <summary>Open a raw WebSocket connection used to upload chunks.</summary>
        /// <param name="endpointPath">Relative path of the WebSocket endpoint.</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="tagId">Associate upload with an existing Tag UID.</param>
        /// <param name="internalBufferSize">Size of the internal buffer.</param>
        /// <param name="receiveBufferSize">Size of the receive buffer.</param>
        /// <param name="sendBufferSize">Size of the send buffer.</param>
        /// <returns>The opened WebSocket connection</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<WebSocket> OpenChunkUploadWebSocketConnectionAsync(
            string endpointPath,
            PostageBatchId batchId,
            TagId? tagId,
            int internalBufferSize,
            int receiveBufferSize,
            int sendBufferSize,
            CancellationToken cancellationToken);

        /// <summary>Rebroadcast existing transaction.</summary>
        /// <param name="txHash">Hash of the transaction.</param>
        /// <returns>Hash of the transaction</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<string> RebroadcastTransactionAsync(
            EthTxHash txHash,
            CancellationToken cancellationToken = default);

        /// <summary>Get current status of node in redistribution game.</summary>
        /// <returns>Redistribution status info</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<RedistributionState> RedistributionStateAsync(CancellationToken cancellationToken = default);

        /// <summary>Reupload a root hash to the network.</summary>
        /// <param name="reference">Swarm content reference.</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="redundancyLevel">The redundancy level of the content, used to traverse redundant data.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task ReuploadContentAsync(
            SwarmReference reference,
            PostageBatchId batchId,
            RedundancyLevel? redundancyLevel = null,
            CancellationToken cancellationToken = default);

        /// <summary>Send to recipient or target with Postal Service for Swarm.</summary>
        /// <param name="topic">Topic name.</param>
        /// <param name="targets">Target message address prefix. If multiple targets are specified, only one would be matched.</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="recipient">Recipient public key.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task SendPssAsync(
            string topic,
            string targets,
            PostageBatchId batchId,
            string? recipient = null,
            CancellationToken cancellationToken = default);

        /// <summary>Enable or disable participation in new redistribution rounds.</summary>
        /// <remarks>
        /// Sampling still runs while disabled, so a later re-enable can commit in the following commit phase. Disabling does not abort an in-flight commit and does not skip reveal or claim for a round that already committed. Re-enabling during a commit phase that was already skipped does not retry that round. After a node restart participation is enabled again.
        /// </remarks>
        /// <param name="enabled">Whether the node should commit in new redistribution rounds.</param>
        /// <returns>The updated participation flag</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<bool> SetRedistributionParticipationAsync(
            bool enabled,
            CancellationToken cancellationToken = default);

        /// <summary>Set P2P welcome message.</summary>
        /// <param name="welcomeMessage">The welcome message to set.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task SetWelcomeMessageAsync(
            string welcomeMessage,
            CancellationToken cancellationToken = default);

        /// <summary>Withdraw all staked amount.</summary>
        /// <remarks>
        /// Be aware, this endpoint creates an on-chain transactions and transfers BZZ from the node's Ethereum account and hence directly manipulates the wallet balance.
        /// </remarks>
        /// <param name="gasPrice">Gas price for transaction.</param>
        /// <param name="gasLimit">Gas limit for transaction.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task StakeDeleteAsync(
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default);

        /// <summary>Get the staked amount and the minimum additional deposit.</summary>
        /// <remarks>
        /// This endpoint fetches the total staked amount and the minimum additional deposit from the blockchain. The first deposit is at least 10 BZZ times 2^height. Subsequent deposits are at least 1 PLUR, or more if the price oracle has increased since the last deposit.
        /// </remarks>
        /// <returns>Stake info</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<StakeInfo> StakeGetAsync(CancellationToken cancellationToken = default);

        /// <summary>Deposit some amount for staking.</summary>
        /// <remarks>
        /// Be aware, this endpoint creates an on-chain transactions and transfers BZZ from the node's Ethereum account and hence directly manipulates the wallet balance.
        /// </remarks>
        /// <param name="amount">Amount of BZZ added that will be deposited for staking.</param>
        /// <param name="gasPrice">Gas price for transaction.</param>
        /// <param name="gasLimit">Gas limit for transaction.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task StakePostAsync(
            BzzValue amount,
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default);

        /// <summary>Withdraw the extra withdrawable staked amount.</summary>
        /// <remarks>
        /// This endpoint withdraws any amount that is possible to withdraw as surplus.
        /// </remarks>
        /// <param name="gasPrice">Gas price for transaction.</param>
        /// <param name="gasLimit">Gas limit for transaction.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task StakeWithdrawableDeleteAsync(XDaiValue? gasPrice = null, ulong? gasLimit = null, CancellationToken cancellationToken = default);

        /// <summary>Get the withdrawable staked amount.</summary>
        /// <remarks>
        /// This endpoint fetches any amount that is possible to withdraw as surplus.
        /// </remarks>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task StakeWithdrawableGetAsync(CancellationToken cancellationToken = default);

        /// <summary>Get the current status snapshot of this node.</summary>
        /// <returns>Returns the current node status snapshot.</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<StatusNode> StatusNodeAsync(CancellationToken cancellationToken = default);

        /// <summary>Get the current status snapshot of this node connected peers.</summary>
        /// <returns>Returns the status snapshot of this node connected peers</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<StatusNode[]> StatusPeersAsync(CancellationToken cancellationToken = default);

        /// <summary>Subscribe to GSOC payloads.</summary>
        /// <param name="reference">Single Owner Chunk address (which may have multiple payloads).</param>
        /// <param name="socFields">Single Owner Chunk fields to be serialized and channeled on every incoming GSOC message, in the given order. When omitted it defaults to the payload. To have random access on the response bytes, put the payload last since it has variable length.</param>
        /// <param name="cacheWrappedChunk">Indicates whether the wrapped chunk of every incoming GSOC message should be cached locally so that it can be resolved through the bytes endpoint (useful when the single owner chunk wraps a root chunk larger than 4KB).</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task SubscribeToGsocAsync(
            string reference,
            IEnumerable<SwarmSocField>? socFields = null,
            bool? cacheWrappedChunk = null,
            CancellationToken cancellationToken = default);

        /// <summary>Subscribe for messages on the given topic.</summary>
        /// <param name="topic">Topic name.</param>
        /// <returns>Returns a WebSocket with a subscription for incoming message data on the requested topic.</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task SubscribeToPssAsync(
            string topic,
            CancellationToken cancellationToken = default);

        /// <summary>Top up an existing postage batch.</summary>
        /// <param name="batchId">Batch ID to top up.</param>
        /// <param name="amount">Amount of BZZ per chunk to top up to an existing postage batch.</param>
        /// <param name="gasPrice">Gas price for transaction.</param>
        /// <param name="gasLimit">Gas limit for transaction.</param>
        /// <returns>Returns the tx hash updating the postage batch</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<EthTxHash> TopUpPostageBatchAsync(
            PostageBatchId batchId,
            BzzValue amount,
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default);

        /// <summary>Try connection to node.</summary>
        /// <param name="peerId">Swarm address of peer.</param>
        /// <returns>Returns round trip time for given peer</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<string> TryConnectToPeerAsync(
            string peerId,
            CancellationToken cancellationToken = default);

        /// <summary>Find feed update.</summary>
        /// <param name="owner">Owner.</param>
        /// <param name="topic">Topic.</param>
        /// <param name="at">Timestamp of the update (default: now).</param>
        /// <param name="after">Start index (default: 0).</param>
        /// <param name="afterLevel">Start level for the feed lookup.</param>
        /// <param name="type">Feed indexing scheme (default: sequence).</param>
        /// <param name="onlyRootChunk">Returns only the root chunk of the content.</param>
        /// <param name="cache">Determines if the download data should be cached on the node. By default the download will be cached.</param>
        /// <param name="redundancyStrategy">Specify the retrieve strategy on redundant data.</param>
        /// <param name="redundancyFallbackMode">Specify if the retrieve strategies (chunk prefetching on redundant data) are used in a fallback cascade. The default is true.</param>
        /// <param name="chunkRetrievalTimeout">Specify the timeout for chunk retrieval. The default is 30 seconds.</param>
        /// <returns>Latest feed update</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<FileResponse?> TryGetFeedAsync(
            EthAddress owner,
            SwarmFeedTopic topic,
            long? at = null,
            ulong? after = null,
            int? afterLevel = null,
            SwarmFeedType type = SwarmFeedType.Sequence,
            bool? onlyRootChunk = null,
            bool? cache = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyFallbackMode = null,
            string? chunkRetrievalTimeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>Try to get file headers.</summary>
        /// <param name="address">Swarm address of content.</param>
        /// <param name="actTimestamp">ACT history Unix timestamp.</param>
        /// <param name="actPublisher">ACT content publisher's public key.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <param name="redundancyLevel">The redundancy level of the content, used to determine the retrieve strategy on redundant data.</param>
        /// <param name="redundancyStrategy">Specify the retrieve strategy on redundant data.</param>
        /// <param name="redundancyStrategyFallback">Specify if the retrieve strategies are used in a fallback cascade.</param>
        /// <param name="cache">Determines if the download data should be cached on the node. By default the download will be cached.</param>
        /// <param name="chunkRetrievalTimeout">Specify the timeout for chunk retrieval. The default is 30 seconds.</param>
        /// <returns>Headers with values</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<HttpContentHeaders?> TryGetFileHeadersAsync(
            SwarmAddress address,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            RedundancyLevel? redundancyLevel = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyStrategyFallback = null,
            bool? cache = null,
            string? chunkRetrievalTimeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>Try to get the byte length of a file.</summary>
        /// <param name="address">Swarm address of content.</param>
        /// <param name="actTimestamp">ACT history Unix timestamp.</param>
        /// <param name="actPublisher">ACT content publisher's public key.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <param name="redundancyLevel">The redundancy level of the content, used to determine the retrieve strategy on redundant data.</param>
        /// <param name="redundancyStrategy">Specify the retrieve strategy on redundant data.</param>
        /// <param name="redundancyStrategyFallback">Specify if the retrieve strategies are used in a fallback cascade.</param>
        /// <param name="cache">Determines if the download data should be cached on the node. By default the download will be cached.</param>
        /// <param name="chunkRetrievalTimeout">Specify the timeout for chunk retrieval. The default is 30 seconds.</param>
        /// <returns>Byte size of file</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<long?> TryGetFileSizeAsync(
            SwarmAddress address,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            RedundancyLevel? redundancyLevel = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyStrategyFallback = null,
            bool? cache = null,
            string? chunkRetrievalTimeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>Update the label of an existing postage batch.</summary>
        /// <param name="batchId">Postage batch ID.</param>
        /// <param name="label">New label for the postage batch.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task UpdatePostageBatchLabelAsync(
            PostageBatchId batchId,
            string label,
            CancellationToken cancellationToken = default);

        /// <summary>Update total count and swarm hash for a tag of an input stream of unknown size, by ID.</summary>
        /// <param name="id">Tag ID.</param>
        /// <param name="hash">Can contain swarm hash to use for the tag.</param>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task UpdateTagAsync(
            TagId id,
            SwarmHash? hash = null,
            CancellationToken cancellationToken = default);

        /// <summary>Upload data.</summary>
        /// <param name="body">Data stream to upload.</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="compactLevel">Compaction level to apply to the uploaded data.</param>
        /// <param name="tagId">Associate upload with an existing Tag UID.</param>
        /// <param name="pin">Represents if the uploaded data should be also locally pinned on the node.</param>
        /// <param name="encrypt">Represents the encrypting state of the file.</param>
        /// <param name="act">Determines if the uploaded data should be treated as ACT (Access Control Trie) content.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <param name="deferredUpload">Determines if the uploaded data should be sent to the network immediately or in a deferred fashion. By default the upload will be deferred.</param>
        /// <param name="redundancyLevel">Add redundancy to the data being uploaded so that downloaders can download it with better UX. 0 value is default and does not add any redundancy to the file.</param>
        /// <param name="maxUploadAttempts">Maximum number of upload attempts (initial try plus retries) on transient failures. 1 disables retrying. Requires a seekable <paramref name="body"/> when greater than 1.</param>
        /// <returns>Content reference</returns>
        /// <remarks>Retrying reuses the same postage batch. For plain uploads this is idempotent, since the content addresses are deterministic. When <paramref name="encrypt"/> is true the content is re-encrypted with a fresh random key on each attempt, so a retry produces a different reference and consumes additional batch utilization.</remarks>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SwarmReference> UploadBytesAsync(
            Stream body,
            PostageBatchId batchId,
            ushort? compactLevel = 0,
            TagId? tagId = null,
            bool? pin = null,
            bool? encrypt = null,
            bool? act = null,
            string? actHistoryAddress = null,
            bool? deferredUpload = null,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            int maxUploadAttempts = 1,
            CancellationToken cancellationToken = default);

        /// <summary>Upload a chunk.</summary>
        /// <param name="chunkData">Chunk data stream to upload.</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="tagId">Associate upload with an existing Tag UID.</param>
        /// <param name="presignedPostageStamp">Pre-signed postage stamp for the corresponding chunk. Required when no postage batch ID is supplied.</param>
        /// <returns>Content reference</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SwarmHash> UploadChunkAsync(
            Stream chunkData,
            PostageBatchId? batchId,
            TagId? tagId = null,
            PostageStamp? presignedPostageStamp = null,
            CancellationToken cancellationToken = default);

        /// <summary>Upload a chunk.</summary>
        /// <param name="chunk">Chunk to upload.</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="tagId">Associate upload with an existing Tag UID.</param>
        /// <param name="presignedPostageStamp">Pre-signed postage stamp for the corresponding chunk. Required when no postage batch ID is supplied.</param>
        /// <returns>Content reference</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SwarmHash> UploadChunkAsync(
            SwarmCac chunk,
            PostageBatchId? batchId,
            TagId? tagId = null,
            PostageStamp? presignedPostageStamp = null,
            CancellationToken cancellationToken = default);

        /// <summary>Upload a directory.</summary>
        /// <param name="directoryPath">The directory path.</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="compactLevel">Compaction level to apply to the uploaded data.</param>
        /// <param name="tagId">Associate upload with an existing Tag UID.</param>
        /// <param name="pin">Represents if the uploaded data should be also locally pinned on the node.</param>
        /// <param name="encrypt">Represents the encrypting state of the file.</param>
        /// <param name="indexDocument">Default file to serve when a directory path is accessed.</param>
        /// <param name="errorDocument">Custom error document to return when a path is not found in the collection.</param>
        /// <param name="deferredUpload">Determines if the uploaded data should be sent to the network immediately or in a deferred fashion. By default the upload will be deferred.</param>
        /// <param name="redundancyLevel">Add redundancy to the data being uploaded so that downloaders can download it with better UX. 0 value is default and does not add any redundancy to the file.</param>
        /// <param name="act">Determines if the uploaded data should be treated as ACT (Access Control Trie) content.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <param name="maxUploadAttempts">Maximum number of upload attempts (initial try plus retries) on transient failures. 1 disables retrying.</param>
        /// <returns>Content reference</returns>
        /// <remarks>Retrying reuses the same postage batch. For plain uploads this is idempotent, since the content addresses are deterministic. When <paramref name="encrypt"/> is true the content is re-encrypted with a fresh random key on each attempt, so a retry produces a different reference and consumes additional batch utilization.</remarks>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SwarmReference> UploadDirectoryAsync(
            string directoryPath,
            PostageBatchId batchId,
            ushort? compactLevel = 0,
            TagId? tagId = null,
            bool? pin = null,
            bool? encrypt = null,
            string? indexDocument = null,
            string? errorDocument = null,
            bool? deferredUpload = null,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            bool? act = null,
            string? actHistoryAddress = null,
            int maxUploadAttempts = 1,
            CancellationToken cancellationToken = default);

        /// <summary>Publish a feed update.</summary>
        /// <param name="topic">Feed topic.</param>
        /// <param name="type">Feed indexing scheme.</param>
        /// <param name="data">Data payload of the feed update, passed to the feed as-is.</param>
        /// <param name="signer">Signer of the feed owner. Its address identifies the feed owner.</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="knownNearIndex">An optional known feed index near the last one, to speed up lookup.</param>
        /// <param name="tagId">Tag UID.</param>
        /// <param name="deferredUpload">Determines if data should be uploaded in a deferred way.</param>
        /// <param name="pin">Represents if the uploaded data should be also locally pinned on the node.</param>
        /// <param name="timestamp">Optional timestamp for the feed update (used by epoch feeds).</param>
        /// <returns>Reference hash of the published feed chunk</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SwarmHash> UpdateFeedAsync(
            SwarmFeedTopic topic,
            SwarmFeedType type,
            ReadOnlyMemory<byte> data,
            ISigner signer,
            PostageBatchId batchId,
            SwarmFeedIndexBase? knownNearIndex = null,
            TagId? tagId = null,
            bool deferredUpload = false,
            bool? pin = null,
            DateTimeOffset? timestamp = null,
            CancellationToken cancellationToken = default);

        /// <summary>Upload feed root manifest.</summary>
        /// <param name="feed">Feed.</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="compactLevel">Compaction level to apply to the uploaded data.</param>
        /// <param name="pin">Represents if the uploaded data should be also locally pinned on the node.</param>
        /// <param name="act">Determines if the uploaded data should be treated as ACT (Access Control Trie) content.</param>
        /// <param name="actHistoryAddress">ACT history reference address.</param>
        /// <param name="redundancyLevel">Add redundancy to the feed manifest being uploaded so that downloaders can download it with better UX. 0 value is default and does not add any redundancy.</param>
        /// <returns>Reference hash</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SwarmHash> UploadFeedManifestAsync(
            SwarmFeedBase feed,
            PostageBatchId batchId,
            ushort? compactLevel = 0,
            bool pin = false,
            bool? act = null,
            string? actHistoryAddress = null,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            CancellationToken cancellationToken = default);

        /// <summary>Upload a file.</summary>
        /// <param name="content">Input file content.</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="compactLevel">Compaction level to apply to the uploaded data.</param>
        /// <param name="name">Filename when uploading single file.</param>
        /// <param name="contentType">The specified content-type is preserved for download of the asset.</param>
        /// <param name="isFileCollection">Upload file/files as a collection.</param>
        /// <param name="tagId">Associate upload with an existing Tag UID.</param>
        /// <param name="pin">Represents if the uploaded data should be also locally pinned on the node.</param>
        /// <param name="encrypt">Represents the encrypting state of the file.</param>
        /// <param name="indexDocument">Default file to serve when a directory path is accessed.</param>
        /// <param name="errorDocument">Custom error document to return when a path is not found in the collection.</param>
        /// <param name="deferredUpload">Determines if the uploaded data should be sent to the network immediately or in a deferred fashion. By default the upload will be deferred.</param>
        /// <param name="redundancyLevel">Add redundancy to the data being uploaded so that downloaders can download it with better UX. 0 value is default and does not add any redundancy to the file.</param>
        /// <param name="maxUploadAttempts">Maximum number of upload attempts (initial try plus retries) on transient failures. 1 disables retrying. Requires a seekable <paramref name="content"/> when greater than 1.</param>
        /// <returns>Content reference</returns>
        /// <remarks>Retrying reuses the same postage batch. For plain uploads this is idempotent, since the content addresses are deterministic. When <paramref name="encrypt"/> is true the content is re-encrypted with a fresh random key on each attempt, so a retry produces a different reference and consumes additional batch utilization.</remarks>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SwarmReference> UploadFileAsync(
            Stream content,
            PostageBatchId batchId,
            ushort? compactLevel = 0,
            string? name = null,
            string? contentType = null,
            bool isFileCollection = false,
            TagId? tagId = null,
            bool? pin = null,
            bool? encrypt = null,
            string? indexDocument = null,
            string? errorDocument = null,
            bool? deferredUpload = null,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            int maxUploadAttempts = 1,
            CancellationToken cancellationToken = default);

        /// <summary>Upload single owner chunk.</summary>
        /// <param name="soc">Single owner chunk.</param>
        /// <param name="batchId">ID of Postage Batch that is used to upload data with.</param>
        /// <param name="presignedPostageStamp">Pre-signed postage stamp for the corresponding chunk. Required when no postage batch ID is supplied.</param>
        /// <param name="tagId">Associate upload with an existing Tag UID.</param>
        /// <param name="deferredUpload">Determines if the uploaded data should be sent to the network immediately or in a deferred fashion. By default the upload will be deferred.</param>
        /// <param name="pin">Represents if the uploaded data should be also locally pinned on the node.</param>
        /// <returns>Reference hash</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<SwarmHash> UploadSocAsync(
            SwarmSoc soc,
            PostageBatchId? batchId,
            PostageStamp? presignedPostageStamp = null,
            TagId? tagId = null,
            bool deferredUpload = false,
            bool? pin = null,
            CancellationToken cancellationToken = default);

        /// <summary>Allows withdrawals of BZZ to provided (whitelisted) address.</summary>
        /// <param name="amount">Amount of BZZ to withdraw.</param>
        /// <param name="address">Whitelisted destination address.</param>
        /// <returns>Tx hash</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<EthTxHash> WalletBzzWithdrawAsync(
            BzzValue amount,
            EthAddress address,
            CancellationToken cancellationToken = default);

        /// <summary>Allows withdrawals of xDAI to provided (whitelisted) address.</summary>
        /// <param name="amount">Amount of xDAI to withdraw.</param>
        /// <param name="address">Whitelisted destination address.</param>
        /// <returns>Tx hash</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<EthTxHash> WalletNativeCoinWithdrawAsync(
            XDaiValue amount,
            EthAddress address,
            CancellationToken cancellationToken = default);

        /// <summary>Withdraw tokens from the chequebook to the overlay address.</summary>
        /// <param name="amount">Amount of tokens to withdraw.</param>
        /// <param name="gasPrice">Gas price for transaction.</param>
        /// <returns>Transaction hash of the withdraw transaction</returns>
        /// <exception cref="SwarmSdkApiException">A server side error occurred.</exception>
        Task<EthTxHash> WithdrawFromChequebookAsync(
            BzzValue amount,
            XDaiValue? gasPrice = null,
            CancellationToken cancellationToken = default);
    }
}
