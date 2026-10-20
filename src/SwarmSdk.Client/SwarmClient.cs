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

using Etherna.SwarmSdk.Clients.Bee;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Extensions;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Services;
using Etherna.SwarmSdk.Stores;
using Etherna.SwarmSdk.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Tar;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChainState = Etherna.SwarmSdk.Models.ChainState;
using ChequebookBalance = Etherna.SwarmSdk.Models.ChequebookBalance;
using FileParameter = Etherna.SwarmSdk.Clients.Beehive.FileParameter;
using FileResponse = Etherna.SwarmSdk.Models.FileResponse;
using Loggers = Etherna.SwarmSdk.Models.Loggers;
using PostageBatch = Etherna.SwarmSdk.Models.PostageBatch;
using PostageProof = Etherna.SwarmSdk.Models.PostageProof;
using RedundancyLevel = Etherna.SwarmSdk.Models.RedundancyLevel;
using RedundancyStrategy = Etherna.SwarmSdk.Models.RedundancyStrategy;
using ReserveState = Etherna.SwarmSdk.Models.ReserveState;
using Settlement = Etherna.SwarmSdk.Models.Settlement;
using SwarmFeedType = Etherna.SwarmSdk.Models.SwarmFeedType;
using SwarmReference = Etherna.SwarmSdk.Models.SwarmReference;
using Verbosity = Etherna.SwarmSdk.Models.Verbosity;

namespace Etherna.SwarmSdk
{
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings")]
    public class SwarmClient : ISwarmClient, IDisposable
    {
        // Consts.
        public const int ChunkStreamWSInternalBufferSize = 2 * ChunkStreamWSReceiveBufferSize + ChunkStreamWSSendBufferSize + 256 + 20;
        public const int ChunkStreamWSReceiveBufferSize = SwarmSoc.MaxSocSize;
        public const int ChunkStreamWSSendBufferSize = SwarmSoc.MaxSocSize;
        public const int DefaultPort = 1633;
        public readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

        private static readonly BzzValue MinChainPrice = BzzValue.FromPlurDecimal(1);
        private readonly double TimeSpanMaxSeconds = TimeSpan.MaxValue.TotalSeconds;

        // Fields.
        private readonly Clients.Bee.BeeGeneratedClient beeGeneratedClient = null!;
        private readonly Clients.Beehive.BeehiveGeneratedClient beehiveGeneratedClient = null!;
        private readonly HttpClient httpClient;
        
        private bool disposed;

        // Constructor.
        public SwarmClient(
            Uri nodeUrl,
            SwarmClients apiCompatibility = SwarmClients.Bee,
            HttpClient? httpClient = null,
            bool isDryMode = false)
        {
            ArgumentNullException.ThrowIfNull(nodeUrl);
            
            this.httpClient = httpClient ?? new HttpClient { Timeout = DefaultTimeout };

            ApiCompatibility = apiCompatibility;
            switch (apiCompatibility)
            {
                case SwarmClients.Bee:
                    beeGeneratedClient = new Clients.Bee.BeeGeneratedClient(this.httpClient) { BaseUrl = nodeUrl.ToString() };
                    break;
                case SwarmClients.Beehive:
                    beehiveGeneratedClient = new Clients.Beehive.BeehiveGeneratedClient(nodeUrl.ToString(), this.httpClient);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(apiCompatibility), apiCompatibility, null);
            }
            IsDryMode = isDryMode;
            NodeUrl = nodeUrl;
        }

        // Dispose.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            // Dispose managed resources.
            if (disposing)
                httpClient.Dispose();

            disposed = true;
        }

        // Properties.
        public SwarmClients ApiCompatibility { get; }
        public bool IsDryMode { get; }
        public Uri NodeUrl { get; }
        
        // Methods.
        public async Task<Dictionary<string, Account>> AccountingAsync(
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.AccountingAsync(cancellationToken).ConfigureAwait(false)).PeerData
                        .ToDictionary(
                            i => i.Key,
                            i => new Account(
                                Balance: BzzValue.FromPlurString(i.Value.Balance),
                                ConsumedBalance: BzzValue.FromPlurString(i.Value.ConsumedBalance),
                                CurrentThresholdReceived: BzzValue.FromPlurString(i.Value.CurrentThresholdReceived),
                                CurrentThresholdGiven: BzzValue.FromPlurString(i.Value.CurrentThresholdGiven),
                                ThresholdGiven: BzzValue.FromPlurString(i.Value.ThresholdGiven),
                                ThresholdReceived: BzzValue.FromPlurString(i.Value.ThresholdReceived),
                                SurplusBalance: BzzValue.FromPlurString(i.Value.SurplusBalance),
                                ReservedBalance: BzzValue.FromPlurString(i.Value.ReservedBalance),
                                ShadowReservedBalance: BzzValue.FromPlurString(i.Value.ShadowReservedBalance),
                                GhostBalance: BzzValue.FromPlurString(i.Value.GhostBalance)));
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<(PostageBatchId BatchId, EthTxHash TxHash)> BuyPostageBatchAsync(
            BzzValue amount,
            int depth,
            string? label = null,
            bool? immutable = null,
            ulong? gasLimit = null,
            XDaiValue? gasPrice = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return (PostageBatchId.Zero, EthTxHash.Zero);

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var result = await beeGeneratedClient.StampsPostAsync(
                        amount: amount.ToPlurString(),
                        depth: depth,
                        label: label,
                        immutable: immutable,
                        gas_limit: gasLimit,
                        gas_price: (long?)gasPrice?.ToWeiDecimal(),
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    return (result.BatchID, result.TxHash);
                }
                case SwarmClients.Beehive:
                {
                    var result = await beehiveGeneratedClient.StampsPostAsync(
                        amount: amount.ToPlurString(),
                        depth: depth,
                        label: label,
                        immutable: immutable,
                        gas_Limit: gasLimit,
                        gas_Price: gasPrice?.ToWeiString(),
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    return (result.BatchID, result.TxHash);
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<EthTxHash> CashoutChequeForPeerAsync(
            string peerId,
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return EthTxHash.Zero;
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    return (await beeGeneratedClient.ChequebookCashoutPostAsync(
                        peerId,
                        (long?)gasPrice?.ToWeiDecimal(),
                        gasLimit,
                        cancellationToken).ConfigureAwait(false)).TransactionHash;
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<CheckPinsResult> CheckPinsAsync(
            SwarmReference? reference,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.PinsCheckAsync(
                        reference?.ToString(),
                        cancellationToken).ConfigureAwait(false);
                    return new CheckPinsResult(
                        reference: response.Reference,
                        invalid: response.Invalid,
                        missing: response.Missing,
                        total: response.Total);
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }
        
        public async Task ChunksBulkUploadAsync(
            SwarmChunk[] chunks,
            PostageBatchId batchId,
            int maxUploadAttempts = 1,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(chunks);

            if (IsDryMode)
                return;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    foreach (var chunk in chunks)
                    {
                        // Re-sending an already-stamped chunk is idempotent, so each push can be retried safely.
                        var chunkPayload = chunk.GetFullPayloadToByteArray();
                        await UploadRetrier.ExecuteWithRetryAsync(
                            async ct =>
                            {
                                using var memoryStream = new MemoryStream(chunkPayload);
                                await UploadChunkAsync(
                                    memoryStream,
                                    batchId,
                                    cancellationToken: ct).ConfigureAwait(false);
                            },
                            maxAttempts: maxUploadAttempts,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                    break;
                }
                case SwarmClients.Beehive:
                {
                    // Build payload.
                    List<byte> payload = [];
                    foreach (var chunk in chunks)
                    {
                        var chunkBytes = chunk.GetFullPayload();
                        var chunkSizeByteArray = BitConverter.GetBytes((ushort)chunkBytes.Length);

                        //chunk size
                        payload.AddRange(chunkSizeByteArray);

                        //chunk data
                        payload.AddRange(chunkBytes.Span);

                        //check hash
                        payload.AddRange(chunk.Hash.ToByteArray());
                    }

                    var byteArrayPayload = payload.ToArray();

                    // Upload stream. The payload is rebuilt on each attempt because the stream is consumed by the send.
                    await UploadRetrier.ExecuteWithRetryAsync(
                        async ct =>
                        {
                            using var memoryStream = new MemoryStream(byteArrayPayload);
                            await beehiveGeneratedClient.Ev1ChunksBulkUploadAsync(
                                swarm_Postage_Batch_Id: batchId.ToString(),
                                memoryStream,
                                cancellationToken: ct).ConfigureAwait(false);
                        },
                        maxAttempts: maxUploadAttempts,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    break;
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<SwarmOverlayAddress> ConnectToPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return SwarmOverlayAddress.Zero;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    return (await beeGeneratedClient.ConnectAsync(peerAddress, cancellationToken).ConfigureAwait(false))
                        .Address1;
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<bool> CreatePinAsync(
            SwarmReference reference,
            RedundancyLevel? redundancyLevel = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return false;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    try
                    {
                        await beeGeneratedClient.PinsPostAsync(
                            reference: reference.ToString(),
                            swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel?)redundancyLevel,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                        return true;
                    }
                    catch (SwarmSdkApiException e) when (e.StatusCode == 404)
                    {
                        return false;
                    }
                }
                case SwarmClients.Beehive:
                {
                    try
                    {
                        await beehiveGeneratedClient.PinsPostAsync(reference.ToString(), cancellationToken).ConfigureAwait(false);
                        return true;
                    }
                    catch (SwarmSdkApiException e) when (e.StatusCode == 404)
                    {
                        return false;
                    }
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<TagInfo> CreateTagAsync(
            SwarmHash hash,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return new TagInfo(new TagId(0), "", DateTimeOffset.UtcNow, 0, 0, 0, 0, 0);
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.TagsPostAsync(
                        cancellationToken).ConfigureAwait(false);
                    return new TagInfo(
                        Id: new TagId(response.Uid),
                        Address: response.Address,
                        StartedAt: response.StartedAt,
                        Split: response.Split,
                        Seen: response.Seen,
                        Stored: response.Stored,
                        Sent: response.Sent,
                        Synced: response.Synced);
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<DebugChunkStore> DebugChunkStoreAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    var response = await beeGeneratedClient.DebugstoreAsync(
                        cancellationToken).ConfigureAwait(false);
                    return new DebugChunkStore(
                        new DebugChunkStoreUpload(
                            TotalUploaded: response.Upload.TotalUploaded,
                            TotalSynced: response.Upload.TotalSynced,
                            PendingUpload: response.Upload.PendingUpload),
                        new DebugChunkStorePinning(
                            TotalCollections: response.Pinning.TotalCollections,
                            TotalChunks: response.Pinning.TotalChunks),
                        new DebugChunkStoreCache(
                            Size: response.Cache.Size,
                            Capacity: response.Cache.Capacity),
                        new DebugChunkStoreReserve(
                            SizeWithinRadius: response.Reserve.SizeWithinRadius,
                            TotalSize: response.Reserve.TotalSize,
                            Capacity: response.Reserve.Capacity,
                            LastBinIDs: response.Reserve.LastBinIDs,
                            Epoch: response.Reserve.Epoch),
                        new DebugChunkStoreChunkStore(
                            TotalChunks: response.ChunkStore.TotalChunks,
                            SharedSlots: response.ChunkStore.SharedSlots,
                            ReferenceCount: response.ChunkStore.ReferenceCount));
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public Task DeletePeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return Task.CompletedTask;
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return beeGeneratedClient.PeersDeleteAsync(peerAddress, cancellationToken);
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public Task DeletePinAsync(
            SwarmReference reference,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return Task.CompletedTask;
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return beeGeneratedClient.PinsDeleteAsync(reference.ToString(), cancellationToken);
                case SwarmClients.Beehive:
                    return beehiveGeneratedClient.PinsDeleteAsync(reference.ToString(), cancellationToken);
                default:
                    throw new InvalidOperationException();
            }
        }

        public Task DeleteTagAsync(
            TagId id,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return Task.CompletedTask;
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return beeGeneratedClient.TagsDeleteAsync(
                        id: id.Value,
                        cancellationToken: cancellationToken);
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<EthTxHash> DeleteTransactionAsync(
            EthTxHash txHash,
            XDaiValue? gasPrice = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return txHash;
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.TransactionsDeleteAsync(
                        txHash.ToString(),
                        (long?)gasPrice?.ToWeiDecimal(),
                        cancellationToken).ConfigureAwait(false)).TransactionHash;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<EthTxHash> DepositIntoChequebookAsync(
            BzzValue amount,
            XDaiValue? gasPrice = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return EthTxHash.Zero;
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.ChequebookDepositAsync(
                        (long)amount.ToPlurDecimal(),
                        (long?)gasPrice?.ToWeiDecimal(),
                        cancellationToken).ConfigureAwait(false)).TransactionHash;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<EthTxHash> DilutePostageBatchAsync(
            PostageBatchId batchId,
            int depth,
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return EthTxHash.Zero;
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.StampsDiluteAsync(
                        batch_id: batchId.ToString(),
                        depth: depth,
                        gas_price: (long?)gasPrice?.ToWeiDecimal(),
                        gas_limit: gasLimit,
                        cancellationToken: cancellationToken).ConfigureAwait(false)).TxHash;
                case SwarmClients.Beehive:
                    return (await beehiveGeneratedClient.StampsDiluteAsync(
                        batchId: batchId.ToString(),
                        depth: depth,
                        gas_Limit: gasLimit,
                        gas_Price: gasPrice?.ToWeiString(),
                        cancellationToken: cancellationToken).ConfigureAwait(false)).TxHash;
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<EnvelopeResponse> EnvelopeAsync(
            string address,
            PostageBatchId batchId,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return new EnvelopeResponse(
                    EthAddress.Zero,
                    new PostageBucketIndex(0, 0),
                    DateTimeOffset.UtcNow,
                    Array.Empty<byte>());
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.EnvelopeAsync(
                        address: address,
                        swarm_postage_batch_id: batchId.ToString(),
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    return new(
                        response.Issuer,
                        PostageBucketIndex.BuildFromByteArray(response.Index.HexToByteArray()),
                        response.Timestamp.HexToByteArray().UnixTimeNanosecondsToDateTimeOffset(),
                        response.Signature.HexToByteArray());
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<PeerBalance[]> GetAllBalancesAsync(
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.BalancesGetAsync(cancellationToken).ConfigureAwait(false);
                    return response.Balances1.Select(b => new PeerBalance(
                        Peer: b.Peer,
                        Balance: BzzValue.FromPlurString(b.Balance1),
                        ThresholdGiven: BzzValue.FromPlurString(b.Thresholdgiven),
                        ThresholdReceived: BzzValue.FromPlurString(b.Thresholdreceived))).ToArray();
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<ChequebookCheque[]> GetAllChequebookChequesAsync(
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.ChequebookChequeGetAsync(cancellationToken).ConfigureAwait(false);
                    return response.Lastcheques.Select(c =>
                            new ChequebookCheque(
                                peer: c.Peer,
                                lastReceived: c.Lastreceived is not null ? new ChequePayment(
                                    beneficiary: c.Lastreceived.Beneficiary,
                                    chequebook: c.Lastreceived.Chequebook,
                                    payout: BzzValue.FromPlurString(c.Lastreceived.Payout)) : null,
                                lastSent: c.Lastsent is not null ? new ChequePayment(
                                    beneficiary: c.Lastsent.Beneficiary,
                                    chequebook: c.Lastsent.Chequebook,
                                    payout: BzzValue.FromPlurString(c.Lastsent.Payout)) : null))
                        .ToArray();
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<IEnumerable<PeerBalance>> GetAllConsumedBalancesAsync(
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.ConsumedGetAsync(cancellationToken).ConfigureAwait(false);
                    return response.Balances1.Select(b => new PeerBalance(
                        Peer: b.Peer,
                        Balance: BzzValue.FromPlurString(b.Balance1),
                        ThresholdReceived: BzzValue.FromPlurString(b.Thresholdreceived),
                        ThresholdGiven: BzzValue.FromPlurString(b.Thresholdgiven)
                    ));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<(string Address, bool FullNode)[]> GetAllPeerAddressesAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    return (await beeGeneratedClient.PeersGetAsync(cancellationToken).ConfigureAwait(false)).Peers1
                        .Select(i => (i.Address, i.FullNode)).ToArray();
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<SwarmReference[]> GetAllPinsAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.PinsGetAsync(cancellationToken).ConfigureAwait(false);
                    return (response.References ?? []).Select(h => new SwarmReference(h)).ToArray();
                }
                case SwarmClients.Beehive:
                {
                    var response = await beehiveGeneratedClient.PinsGetAsync(cancellationToken).ConfigureAwait(false);
                    return response.References.Select(h => new SwarmReference(h)).ToArray();
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<Settlement> GetAllSettlementsAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.SettlementsGetAsync(cancellationToken).ConfigureAwait(false);
                    return new Settlement(
                        totalReceived: BzzValue.FromPlurString(response.TotalReceived),
                        totalSent: BzzValue.FromPlurString(response.TotalSent),
                        settlements: response.Settlements1
                            .Select(s => new SettlementData(
                                peer: s.Peer,
                                received: BzzValue.FromPlurString(s.Received),
                                sent: BzzValue.FromPlurString(s.Sent))));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<Settlement> GetAllTimeSettlementsAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.TimesettlementsAsync(cancellationToken).ConfigureAwait(false);
                    return new Settlement(
                        totalReceived: BzzValue.FromPlurString(response.TotalReceived),
                        totalSent: BzzValue.FromPlurString(response.TotalSent),
                        settlements: response.Settlements1
                            .Select(s => new SettlementData(
                                peer: s.Peer,
                                received: BzzValue.FromPlurString(s.Received),
                                sent: BzzValue.FromPlurString(s.Sent))));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<PeerBalance> GetBalanceWithPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    var response = await beeGeneratedClient.BalancesGetAsync(peerAddress, cancellationToken).ConfigureAwait(false);
                    return new PeerBalance(
                        Peer: response.Peer,
                        Balance: BzzValue.FromPlurString(response.Balance1),
                        ThresholdReceived: BzzValue.FromPlurString(response.Thresholdreceived),
                        ThresholdGiven: BzzValue.FromPlurString(response.Thresholdgiven));
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<Stream> GetBytesAsync(
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
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.BytesGetAsync(
                        address: reference.ToString(),
                        swarm_cache: cache,
                        swarm_redundancy_strategy: (Clients.Bee.SwarmRedundancyStrategy?)redundancyStrategy,
                        swarm_redundancy_fallback_mode: redundancyFallbackMode,
                        swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel?)redundancyLevel,
                        swarm_chunk_retrieval_timeout: chunkRetrievalTimeout,
                        swarm_lookahead_buffer_size: lookaheadBufferSize,
                        swarm_act_timestamp: actTimestamp,
                        swarm_act_publisher: actPublisher,
                        swarm_act_history_address: actHistoryAddress,
                        cancellationToken: cancellationToken).ConfigureAwait(false)).Stream;
                case SwarmClients.Beehive:
                    return (await beehiveGeneratedClient.BytesGetAsync(
                        reference: reference.ToString(),
                        swarm_Redundancy_Level: (Clients.Beehive.RedundancyLevel?)redundancyLevel,
                        swarm_Redundancy_Strategy: (Clients.Beehive.RedundancyStrategy?)redundancyStrategy,
                        swarm_Redundancy_Fallback_Mode: redundancyFallbackMode,
                        cancellationToken: cancellationToken).ConfigureAwait(false)).Stream;
                default:
                    throw new InvalidOperationException();
            }
        }

        public Task<HttpContentHeaders?> GetBytesHeadersAsync(
            SwarmReference reference,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            bool? cache = null,
            RedundancyLevel? redundancyLevel = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyFallbackMode = null,
            string? chunkRetrievalTimeout = null,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return beeGeneratedClient.BytesHeadAsync(
                        address: reference.ToString(),
                        swarm_cache: cache,
                        swarm_redundancy_strategy: (Clients.Bee.SwarmRedundancyStrategy?)redundancyStrategy,
                        swarm_redundancy_fallback_mode: redundancyFallbackMode,
                        swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel?)redundancyLevel,
                        swarm_chunk_retrieval_timeout: chunkRetrievalTimeout,
                        swarm_act_timestamp: actTimestamp,
                        swarm_act_publisher: actPublisher,
                        swarm_act_history_address: actHistoryAddress,
                        cancellationToken: cancellationToken);
                case SwarmClients.Beehive:
                    return beehiveGeneratedClient.BytesHeadAsync(
                        reference: reference.ToString(),
                        cancellationToken: cancellationToken);
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<BlockedPeer[]> GetBlocklistedPeerAddressesAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.BlocklistAsync(cancellationToken).ConfigureAwait(false))
                        .Peers.Select(i => new BlockedPeer(
                            Address: i.Address,
                            FullNode: i.FullNode,
                            Reason: i.Reason,
                            Duration: i.Duration)).ToArray();
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<ChainState> GetChainStateAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.ChainstateAsync(cancellationToken).ConfigureAwait(false);
                    return new ChainState(
                        Block: response.Block,
                        ChainTip: response.ChainTip,
                        CurrentPrice: ReadChainPrice(response.CurrentPrice),
                        TotalAmount: BzzValue.FromPlurString(response.TotalAmount),
                        MinimumValidityBlocks: response.MinimumValidityBlocks);
                }
                case SwarmClients.Beehive:
                {
                    var response = await beehiveGeneratedClient.ChainstateAsync(cancellationToken).ConfigureAwait(false);
                    return new ChainState(
                        Block: response.Block,
                        ChainTip: response.ChainTip,
                        CurrentPrice: ReadChainPrice(response.CurrentPrice),
                        TotalAmount: BzzValue.FromPlurString(response.TotalAmount),
                        MinimumValidityBlocks: 0);
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<EthAddress> GetChequebookAddressAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.ChequebookAddressAsync(cancellationToken).ConfigureAwait(false))
                        .ChequebookAddress1;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<ChequebookBalance> GetChequebookBalanceAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.ChequebookBalanceAsync(cancellationToken).ConfigureAwait(false);
                    return new ChequebookBalance(
                        totalBalance: BzzValue.FromPlurString(response.TotalBalance),
                        availableBalance: BzzValue.FromPlurString(response.AvailableBalance));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<ChequebookCashout> GetChequebookCashoutForPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.ChequebookCashoutGetAsync(
                        peerAddress,
                        cancellationToken).ConfigureAwait(false);
                    return new ChequebookCashout(
                        peer: response.Peer,
                        lastCashedCheque: response.LastCashedCheque is not null
                            ? new ChequePayment(
                                response.LastCashedCheque.Beneficiary,
                                response.LastCashedCheque.Chequebook,
                                BzzValue.FromPlurString(response.LastCashedCheque.Payout))
                            : null,
                        transactionHash: response.TransactionHash,
                        result: response.Result is not null
                            ? new ResultChequebook(
                                recipient: response.Result.Recipient,
                                lastPayout: BzzValue.FromPlurString(response.Result.LastPayout),
                                bounced: response.Result.Bounced)
                            : null,
                        uncashedAmount: BzzValue.FromPlurString(response.UncashedAmount));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<ChequebookCheque> GetChequebookChequeForPeerAsync(
            string peerId,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.ChequebookChequeGetAsync(peerId, cancellationToken).ConfigureAwait(false);
                    return new ChequebookCheque(
                        peer: response.Peer,
                        lastReceived: response.Lastreceived is not null ? new ChequePayment(
                            beneficiary: response.Lastreceived.Beneficiary,
                            chequebook: response.Lastreceived.Chequebook,
                            payout: BzzValue.FromPlurString(response.Lastreceived.Payout)) : null,
                        lastSent: response.Lastsent is not null ? new ChequePayment(
                            beneficiary: response.Lastsent.Beneficiary,
                            chequebook: response.Lastsent.Chequebook,
                            payout: BzzValue.FromPlurString(response.Lastsent.Payout)) : null);
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<SwarmChunk> GetChunkAsync(
            SwarmHash hash,
            SwarmChunkBmt swarmChunkBmt,
            bool? cache = null,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default)
        {
            using var memoryStream = new MemoryStream();
            var stream = await GetChunkStreamAsync(
                hash,
                cache,
                actTimestamp,
                actPublisher,
                actHistoryAddress,
                cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            }
            var chunkData = memoryStream.ToArray();

            return SwarmChunk.BuildChunkFromHashAndData(hash, chunkData, swarmChunkBmt);
        }

        public async Task<Stream> GetChunkStreamAsync(
            SwarmHash hash,
            bool? cache = null,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.ChunksGetAsync(
                        hash.ToString(),
                        cache,
                        actTimestamp,
                        actPublisher,
                        actHistoryAddress,
                        cancellationToken).ConfigureAwait(false)).Stream;
                case SwarmClients.Beehive:
                    return (await beehiveGeneratedClient.ChunksGetAsync(
                        hash.ToString(),
                        cancellationToken).ConfigureAwait(false)).Stream;
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task GetChunksStreamWebSocketAsync(
            ulong? tagHeader = null,
            ulong? tagQuery = null,
            PostageBatchId? batchId = null,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    await beeGeneratedClient.ChunksStreamAsync(
                        swarm_tagHeader: tagHeader,
                        swarm_tagQuery: tagQuery,
                        swarm_postage_batch_id: batchId?.ToString(),
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    return;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
        public async Task<IChunkWebSocketUploader> GetChunkUploaderWebSocketAsync(
            PostageBatchId batchId,
            TagId? tagId = null,
            CancellationToken cancellationToken = default)
        {
            // Build uploader.
            var webSocket = await OpenChunkUploadWebSocketConnectionAsync(
                "chunks/stream",
                batchId,
                tagId,
                ChunkStreamWSInternalBufferSize,
                ChunkStreamWSReceiveBufferSize,
                ChunkStreamWSSendBufferSize,
                cancellationToken).ConfigureAwait(false);
            return new ChunkWebSocketUploader(webSocket);
        }

        public async Task<PeerBalance> GetConsumedBalanceWithPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    var response = await beeGeneratedClient.ConsumedGetAsync(peerAddress, cancellationToken).ConfigureAwait(false);
                    return new PeerBalance(
                        Peer: response.Peer,
                        Balance: BzzValue.FromPlurString(response.Balance1),
                        ThresholdReceived: BzzValue.FromPlurString(response.Thresholdreceived),
                        ThresholdGiven: BzzValue.FromPlurString(response.Thresholdgiven));
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<FileResponse> GetFileAsync(
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
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    if (!address.HasPath)
                    {
                        var response = await beeGeneratedClient.BzzGetAsync(
                            address: address.Reference.ToString(),
                            swarm_cache: cache,
                            swarm_redundancy_strategy: (Clients.Bee.SwarmRedundancyStrategy?)redundancyStrategy,
                            swarm_redundancy_fallback_mode: redundancyFallbackMode,
                            swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel?)redundancyLevel,
                            swarm_chunk_retrieval_timeout: chunkRetrievalTimeout,
                            swarm_lookahead_buffer_size: lookaheadBufferSize,
                            swarm_act_timestamp: actTimestamp,
                            swarm_act_publisher: actPublisher,
                            swarm_act_history_address: actHistoryAddress,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                        return new FileResponse(
                            response.ContentHeaders,
                            response.Headers,
                            response.Stream);
                    }
                    else
                    {
                        var response = await beeGeneratedClient.BzzGetAsync(
                            address: address.Reference.ToString(),
                            path: address.Path,
                            swarm_redundancy_strategy: (Clients.Bee.SwarmRedundancyStrategy?)redundancyStrategy,
                            swarm_redundancy_fallback_mode: redundancyFallbackMode,
                            swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel?)redundancyLevel,
                            swarm_chunk_retrieval_timeout: chunkRetrievalTimeout,
                            swarm_cache: cache,
                            swarm_lookahead_buffer_size: lookaheadBufferSize,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                        return new FileResponse(
                            response.ContentHeaders,
                            response.Headers,
                            response.Stream);
                    }
                }
                case SwarmClients.Beehive:
                {
                    var response = await beehiveGeneratedClient.BzzGetAsync(
                        address: address.ToString(),
                        swarm_Redundancy_Level: (Clients.Beehive.RedundancyLevel?)redundancyLevel,
                        swarm_Redundancy_Strategy: (Clients.Beehive.RedundancyStrategy?)redundancyStrategy,
                        swarm_Redundancy_Fallback_Mode: redundancyFallbackMode,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    return new FileResponse(
                        response.ContentHeaders,
                        response.Headers,
                        response.Stream);
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<(PostageBatch PostageBatch, EthAddress Owner)[]> GetGlobalValidPostageBatchesAsync(
            PostageBatchId? batchId = null,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    if (batchId.HasValue)
                    {
                        var response = await beeGeneratedClient.BatchesGetAsync(
                            batch_id: batchId.Value.ToString(),
                            cancellationToken).ConfigureAwait(false);
                        return [(
                            new PostageBatch(
                                id: response.BatchID,
                                amount: BzzValue.FromPlurString(response.Value),
                                blockNumber: response.Start,
                                depth: response.Depth,
                                exists: true,
                                isImmutable: response.Immutable,
                                isUsable: true,
                                label: null,
                                ttl: TimeSpan.FromSeconds(Math.Min(response.BatchTTL, TimeSpanMaxSeconds)),
                                utilization: 0),
                            EthAddress.FromString(response.Owner))];
                    }
                    else
                    {
                        var response = await beeGeneratedClient.BatchesGetAsync(cancellationToken).ConfigureAwait(false);
                        return response.Batches.Select(batch => (
                                new PostageBatch(
                                    id: batch.BatchID,
                                    amount: BzzValue.FromPlurString(batch.Value),
                                    blockNumber: batch.Start,
                                    depth: batch.Depth,
                                    exists: true,
                                    isImmutable: batch.Immutable,
                                    isUsable: true,
                                    label: null,
                                    ttl: TimeSpan.FromSeconds(Math.Min(batch.BatchTTL, TimeSpanMaxSeconds)),
                                    utilization: 0),
                                EthAddress.FromString(batch.Owner)))
                            .ToArray();
                    }
                }
                case SwarmClients.Beehive:
                {
                    var response = await beehiveGeneratedClient.BatchesAsync(cancellationToken).ConfigureAwait(false);
                    return (response.Batches ?? []).Select(batch => (
                            new PostageBatch(
                                id: batch.BatchID,
                                amount: BzzValue.FromPlurString(batch.Value),
                                blockNumber: batch.Start,
                                depth: batch.Depth,
                                exists: true,
                                isImmutable: batch.Immutable,
                                isUsable: true,
                                label: null,
                                ttl: TimeSpan.FromSeconds(Math.Min(batch.BatchTTL, TimeSpanMaxSeconds)),
                                utilization: 0),
                            batch.Owner == null ? EthAddress.Zero : EthAddress.FromString(batch.Owner)))
                        .ToArray();
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<Health> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.HealthAsync(cancellationToken).ConfigureAwait(false);
                    return new(
                        isStatusOk: response.Status switch
                        {
                            Clients.Bee.HealthStatusStatus.Ok => true,
                            Clients.Bee.HealthStatusStatus.Nok => false,
                            _ => throw new InvalidOperationException()
                        },
                        version: response.Version,
                        apiVersion: response.ApiVersion);
                }
                case SwarmClients.Beehive:
                {
                    var response = await beehiveGeneratedClient.HealthAsync(cancellationToken).ConfigureAwait(false);
                    return new(
                        isStatusOk: response.Status switch
                        {
                            "ok" => true,
                            "nok" => false,
                            _ => throw new InvalidOperationException()
                        },
                        version: response.Version,
                        apiVersion: response.ApiVersion);
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<NeighborhoodStatus[]> GetNeighborhoodsStatus(
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.StatusNeighborhoodsAsync(cancellationToken).ConfigureAwait(false);
                    return response.Neighborhoods.Select(s => new NeighborhoodStatus(
                        s.Neighborhood,
                        s.Proximity,
                        s.ReserveSizeWithinRadius)).ToArray();
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<SwarmNodeAddresses> GetNodeAddressesAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.AddressesAsync(cancellationToken).ConfigureAwait(false);
                    return new SwarmNodeAddresses(
                        underlay: response.Underlay.Where(i => !string.IsNullOrWhiteSpace(i)),
                        overlay: response.Overlay,
                        ethereum: response.Ethereum,
                        chainAddress: response.Chain_address,
                        publicKey: response.PublicKey,
                        pssPublicKey: response.PssPublicKey);
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<NodeInfo> GetNodeInfoAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.NodeAsync(cancellationToken).ConfigureAwait(false);
                    return new(
                        beeMode: response.BeeMode switch
                        {
                            Clients.Bee.NodeBeeMode.Full => InfoBeeMode.Full,
                            Clients.Bee.NodeBeeMode.Light => InfoBeeMode.Light,
                            _ => throw new InvalidOperationException()
                        },
                        chequebookEnabled: response.ChequebookEnabled,
                        swapEnabled: response.SwapEnabled);
                }
                case SwarmClients.Beehive:
                {
                    var response = await beehiveGeneratedClient.NodeAsync(cancellationToken).ConfigureAwait(false);
                    return new(
                        beeMode: response.BeeMode switch
                        {
                            "dev" => InfoBeeMode.Dev,
                            "full" => InfoBeeMode.Full,
                            "light" => InfoBeeMode.Light,
                            _ => throw new InvalidOperationException()
                        },
                        chequebookEnabled: response.ChequebookEnabled,
                        swapEnabled: response.SwapEnabled);
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<PostageBatch[]> GetOwnedPostageBatchesAsync(
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.StampsGetAsync(cancellationToken).ConfigureAwait(false);
                    return response.Stamps.Select(b =>
                            new PostageBatch(
                                amount: b.Amount != null ? BzzValue.FromPlurString(b.Amount) : null,
                                depth: b.Depth,
                                blockNumber: b.BlockNumber,
                                exists: b.Exists,
                                id: b.BatchID,
                                isImmutable: b.ImmutableFlag,
                                label: b.Label,
                                ttl: TimeSpan.FromSeconds(Math.Min(b.BatchTTL, TimeSpanMaxSeconds)),
                                isUsable: b.Usable,
                                utilization: (uint)b.Utilization))
                        .ToArray();
                }
                case SwarmClients.Beehive:
                {
                    var response = await beehiveGeneratedClient.StampsGetAsync(cancellationToken).ConfigureAwait(false);
                    return (response.Stamps ?? []).Select(b =>
                            new PostageBatch(
                                amount: b.Amount != null ? BzzValue.FromPlurString(b.Amount) : null,
                                depth: b.Depth,
                                blockNumber: b.BlockNumber,
                                exists: b.Exists,
                                id: b.BatchID,
                                isImmutable: b.ImmutableFlag,
                                label: b.Label,
                                ttl: TimeSpan.FromSeconds(Math.Min(b.BatchTTL, TimeSpanMaxSeconds)),
                                isUsable: b.Usable,
                                utilization: b.Utilization))
                        .ToArray();
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<GnosisChainTx[]> GetPendingTransactionsAsync(
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.TransactionsGetAsync(cancellationToken).ConfigureAwait(false);
                    return response.PendingTransactions.Select(tx => new GnosisChainTx(
                            transactionHash: tx.TransactionHash,
                            to: tx.To,
                            nonce: tx.Nonce,
                            gasPrice: XDaiValue.FromWeiString(tx.GasPrice),
                            gasLimit: tx.GasLimit,
                            data: tx.Data,
                            created: tx.Created,
                            description: tx.Description,
                            value: XDaiValue.FromWeiString(tx.Value)))
                        .ToArray();
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<PinStatus> GetPinStatusAsync(
            SwarmReference reference,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    try
                    {
                        var beePinStatus = await beeGeneratedClient.PinsGetAsync(reference.ToString(), cancellationToken).ConfigureAwait(false);
                        return new PinStatus(
                            reference,
                            creationTime: null,
                            missingChunks: [],
                            processed: beePinStatus.Reference == reference ? true : null,
                            succeeded: beePinStatus.Reference == reference,
                            pinnedChunks: null);
                    }
                    catch (SwarmSdkApiException e) when (e.StatusCode == 404)
                    {
                        return new PinStatus(
                            reference,
                            creationTime: null,
                            missingChunks: null,
                            processed: null,
                            succeeded: false,
                            pinnedChunks: null);
                    }
                }
                case SwarmClients.Beehive:
                {
                    try
                    {
                        var beehivePinStatus = await beehiveGeneratedClient.Ev1PinsGetAsync(reference.ToString(), cancellationToken).ConfigureAwait(false);
                        return new PinStatus(
                            reference,
                            creationTime: DateTimeOffset.FromUnixTimeSeconds(beehivePinStatus.CreationTime),
                            missingChunks: beehivePinStatus.MissingChunks.Select(SwarmHash.FromString),
                            processed: beehivePinStatus.Processed,
                            succeeded: beehivePinStatus.Succeeded,
                            pinnedChunks: beehivePinStatus.PinnedChunks);
                    }
                    catch (SwarmSdkApiException e) when (e.StatusCode == 404)
                    {
                        return new PinStatus(
                            reference,
                            creationTime: null,
                            missingChunks: null,
                            processed: null,
                            succeeded: false,
                            pinnedChunks: null);
                    }
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<PostageBatch> GetPostageBatchAsync(
            PostageBatchId batchId,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.StampsGetAsync(
                        batchId.ToString(),
                        cancellationToken).ConfigureAwait(false);
                    return new PostageBatch(
                        amount: response.Amount != null ? BzzValue.FromPlurString(response.Amount) : null,
                        depth: response.Depth,
                        blockNumber: response.BlockNumber,
                        exists: response.Exists,
                        id: response.BatchID,
                        isImmutable: response.ImmutableFlag,
                        label: response.Label,
                        ttl: TimeSpan.FromSeconds(Math.Min(response.BatchTTL, TimeSpanMaxSeconds)),
                        isUsable: response.Usable,
                        utilization: (uint)response.Utilization);
                }
                case SwarmClients.Beehive:
                {
                    var response = await beehiveGeneratedClient.StampsGetAsync(
                        batchId.ToString(),
                        cancellationToken).ConfigureAwait(false);
                    return new PostageBatch(
                        amount: response.Amount != null ? BzzValue.FromPlurString(response.Amount) : null,
                        depth: response.Depth,
                        blockNumber: response.BlockNumber,
                        exists: response.Exists,
                        id: response.BatchID,
                        isImmutable: response.ImmutableFlag,
                        label: response.Label,
                        ttl: TimeSpan.FromSeconds(Math.Min(response.BatchTTL, TimeSpanMaxSeconds)),
                        isUsable: response.Usable,
                        utilization: response.Utilization);
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<PostageBucketsStatus> GetPostageBatchBucketsAsync(
            PostageBatchId batchId,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.StampsBucketsAsync(
                        batchId.ToString(),
                        cancellationToken).ConfigureAwait(false);
                    return new(
                        bucketDepth: response.BucketDepth,
                        bucketUpperBound: (uint)response.BucketUpperBound,
                        collisions: response.Buckets.Select(b => (uint)b.Collisions),
                        depth: response.Depth);
                }
                case SwarmClients.Beehive:
                {
                    var response = await beehiveGeneratedClient.StampsBucketsAsync(
                        batchId.ToString(),
                        cancellationToken).ConfigureAwait(false);
                    return new(
                        bucketDepth: response.BucketDepth,
                        bucketUpperBound: (uint)response.BucketUpperBound,
                        collisions: response.Buckets.Select(b => (uint)b.Collisions),
                        depth: response.Depth);
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<bool> GetReadinessAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    try
                    {
                        await beeGeneratedClient.ReadinessAsync(cancellationToken).ConfigureAwait(false);
                        return true;
                    }
                    catch (SwarmSdkApiException e) when (e.StatusCode == 400)
                    {
                        return false;
                    }
                }
                case SwarmClients.Beehive:
                {
                    try
                    {
                        await beehiveGeneratedClient.ReadinessAsync(cancellationToken).ConfigureAwait(false);
                        return true;
                    }
                    catch (SwarmSdkApiException e) when (e.StatusCode == 400)
                    {
                        return false;
                    }
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<ReserveCommitment> GetReserveCommitmentAsync(int depth, string anchor1, string anchor2,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.RchashAsync(depth, anchor1, anchor2, cancellationToken)
                        .ConfigureAwait(false);
                    return new(
                        duration: TimeSpan.FromSeconds(Math.Min(response.DurationSeconds, TimeSpanMaxSeconds)),
                        hash: response.Hash,
                        proof1: new ReserveCommitmentProof(
                            chunkSpan: response.Proofs.Proof1.ChunkSpan,
                            postageProof: new PostageProof(
                                index: response.Proofs.Proof1.PostageProof.Index,
                                postageId: response.Proofs.Proof1.PostageProof.PostageId,
                                signature: response.Proofs.Proof1.PostageProof.Signature,
                                timeStamp: DateTimeOffset.FromUnixTimeSeconds(
                                    long.Parse(response.Proofs.Proof1.PostageProof.TimeStamp,
                                        CultureInfo.InvariantCulture))),
                            proofSegments: response.Proofs.Proof1.ProofSegments ?? Array.Empty<string>(),
                            proofSegments2: response.Proofs.Proof1.ProofSegments2 ?? Array.Empty<string>(),
                            proofSegments3: response.Proofs.Proof1.ProofSegments3 ?? Array.Empty<string>(),
                            proveSegment: response.Proofs.Proof1.ProveSegment,
                            proveSegment2: response.Proofs.Proof1.ProveSegment2,
                            socProof: (response.Proofs.Proof1.SocProof ?? Array.Empty<Clients.Bee.ApiSOCProof>())
                            .Select(p => new Models.SocProof(
                                chunkHash: p.ChunkAddr,
                                identifier: p.Identifier,
                                signature: p.Signature,
                                signer: p.Signer))),
                        proof2: new ReserveCommitmentProof(
                            chunkSpan: response.Proofs.Proof2.ChunkSpan,
                            postageProof: new PostageProof(
                                index: response.Proofs.Proof2.PostageProof.Index,
                                postageId: response.Proofs.Proof2.PostageProof.PostageId,
                                signature: response.Proofs.Proof2.PostageProof.Signature,
                                timeStamp: DateTimeOffset.FromUnixTimeSeconds(
                                    long.Parse(response.Proofs.Proof2.PostageProof.TimeStamp,
                                        CultureInfo.InvariantCulture))),
                            proofSegments: response.Proofs.Proof2.ProofSegments ?? Array.Empty<string>(),
                            proofSegments2: response.Proofs.Proof2.ProofSegments2 ?? Array.Empty<string>(),
                            proofSegments3: response.Proofs.Proof2.ProofSegments3 ?? Array.Empty<string>(),
                            proveSegment: response.Proofs.Proof2.ProveSegment,
                            proveSegment2: response.Proofs.Proof2.ProveSegment2,
                            socProof: (response.Proofs.Proof2.SocProof ?? Array.Empty<Clients.Bee.ApiSOCProof>())
                            .Select(p => new Models.SocProof(
                                chunkHash: p.ChunkAddr,
                                identifier: p.Identifier,
                                signature: p.Signature,
                                signer: p.Signer))),
                        proofLast: new ReserveCommitmentProof(
                            chunkSpan: response.Proofs.ProofLast.ChunkSpan,
                            postageProof: new PostageProof(
                                index: response.Proofs.ProofLast.PostageProof.Index,
                                postageId: response.Proofs.ProofLast.PostageProof.PostageId,
                                signature: response.Proofs.ProofLast.PostageProof.Signature,
                                timeStamp: DateTimeOffset.FromUnixTimeSeconds(
                                    long.Parse(response.Proofs.ProofLast.PostageProof.TimeStamp,
                                        CultureInfo.InvariantCulture))),
                            proofSegments: response.Proofs.ProofLast.ProofSegments ?? Array.Empty<string>(),
                            proofSegments2: response.Proofs.ProofLast.ProofSegments2 ?? Array.Empty<string>(),
                            proofSegments3: response.Proofs.ProofLast.ProofSegments3 ?? Array.Empty<string>(),
                            proveSegment: response.Proofs.ProofLast.ProveSegment,
                            proveSegment2: response.Proofs.ProofLast.ProveSegment2,
                            socProof: (response.Proofs.ProofLast.SocProof ?? Array.Empty<Clients.Bee.ApiSOCProof>())
                            .Select(p => new Models.SocProof(
                                chunkHash: p.ChunkAddr,
                                identifier: p.Identifier,
                                signature: p.Signature,
                                signer: p.Signer))));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<ReserveState> GetReserveStateAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.ReservestateAsync(cancellationToken).ConfigureAwait(false);
                    return new(
                        commitment: response.Commitment,
                        radius: response.Radius,
                        reserveCapacityDoubling: response.ReserveCapacityDoubling,
                        storageRadius: response.StorageRadius);
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<SettlementData> GetSettlementsWithPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.SettlementsGetAsync(peerAddress, cancellationToken).ConfigureAwait(false);
                    return new SettlementData(
                        peer: response.Peer,
                        received: BzzValue.FromPlurString(response.Received),
                        sent: BzzValue.FromPlurString(response.Sent));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<SwarmSoc> GetSocAsync(
            EthAddress owner,
            SwarmSocIdentifier identifier,
            bool? cache = null,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default)
        {
            // The SOC address is the keccak256 of identifier and owner: download the chunk at that address.
            var swarmChunkBmt = new SwarmChunkBmt();
            var address = SwarmSoc.BuildHash(identifier, owner, swarmChunkBmt.Hasher);

            var chunk = await GetChunkAsync(
                address,
                swarmChunkBmt,
                cache,
                actTimestamp,
                actPublisher,
                actHistoryAddress,
                cancellationToken).ConfigureAwait(false);

            if (chunk is not SwarmSoc soc ||
                soc.Owner != owner ||
                soc.Identifier != identifier)
                throw new SwarmChunkVerificationException(
                    address,
                    $"Downloaded chunk at {address} is not the expected single owner chunk");

            return soc;
        }

        public async Task<FileResponse> GetSocDataAsync(
            EthAddress owner,
            string id,
            bool? onlyRootChunk = null,
            bool? cache = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyFallbackMode = null,
            string? chunkRetrievalTimeout = null,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.SocGetAsync(
                        owner: owner.ToString(),
                        id: id,
                        swarm_only_root_chunk: onlyRootChunk,
                        swarm_cache: cache,
                        swarm_redundancy_strategy: (Clients.Bee.SwarmRedundancyStrategy?)redundancyStrategy,
                        swarm_redundancy_fallback_mode: redundancyFallbackMode,
                        swarm_chunk_retrieval_timeout: chunkRetrievalTimeout,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    return new FileResponse(
                        response.ContentHeaders,
                        response.Headers,
                        response.Stream);
                }
                case SwarmClients.Beehive:
                {
                    var response = await beehiveGeneratedClient.SocGetAsync(
                        owner: owner.ToString(),
                        id: id,
                        swarm_Only_Root_Chunk: onlyRootChunk,
                        swarm_Redundancy_Strategy: (Clients.Beehive.RedundancyStrategy?)redundancyStrategy,
                        swarm_Redundancy_Fallback_Mode: redundancyFallbackMode,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    return new FileResponse(
                        response.ContentHeaders,
                        response.Headers,
                        response.Stream);
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<Topology> GetSwarmTopologyAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.TopologyAsync(cancellationToken).ConfigureAwait(false);
                    return new(
                        baseAddr: response.BaseAddr,
                        bins: response.Bins.ToDictionary(
                            i => i.Key,
                            i => new PeersAggregate(
                                population: i.Value.Population,
                                connected: i.Value.Connected,
                                disconnectedPeers: i.Value.DisconnectedPeers.Select(peer => new Peer(
                                    address: peer.Address,
                                    lastSeenTimestamp: peer.Metrics.LastSeenTimestamp,
                                    sessionConnectionRetry: peer.Metrics.SessionConnectionRetry,
                                    connectionTotalDuration: peer.Metrics.ConnectionTotalDuration,
                                    sessionConnectionDuration: peer.Metrics.SessionConnectionDuration,
                                    sessionConnectionDirection: peer.Metrics.SessionConnectionDirection,
                                    latencyEwma: peer.Metrics.LatencyEWMA)),
                                connectedPeers: i.Value.ConnectedPeers.Select(peer => new Peer(
                                    address: peer.Address,
                                    lastSeenTimestamp: peer.Metrics.LastSeenTimestamp,
                                    sessionConnectionRetry: peer.Metrics.SessionConnectionRetry,
                                    connectionTotalDuration: peer.Metrics.ConnectionTotalDuration,
                                    sessionConnectionDuration: peer.Metrics.SessionConnectionDuration,
                                    sessionConnectionDirection: peer.Metrics.SessionConnectionDirection,
                                    latencyEwma: peer.Metrics.LatencyEWMA)))),
                        connected: response.Connected,
                        depth: response.Depth,
                        networkAvailability: response.NetworkAvailability switch
                        {
                            Clients.Bee.BzzTopologyNetworkAvailability.Unknown => NetworkAvailability.Unknown,
                            Clients.Bee.BzzTopologyNetworkAvailability.Available => NetworkAvailability.Available,
                            Clients.Bee.BzzTopologyNetworkAvailability.Unavailable => NetworkAvailability.Unavailable,
                            _ => throw new InvalidOperationException(),
                        },
                        nnLowWatermark: response.NnLowWatermark,
                        population: response.Population,
                        reachability: response.Reachability switch
                        {
                            Clients.Bee.BzzTopologyReachability.Unknown => Reachability.Unknown,
                            Clients.Bee.BzzTopologyReachability.Public => Reachability.Public,
                            Clients.Bee.BzzTopologyReachability.Private => Reachability.Private,
                            _ => throw new InvalidOperationException(),
                        },
                        timestamp: DateTimeOffset.FromUnixTimeSeconds(
                            long.Parse(response.Timestamp, CultureInfo.InvariantCulture)));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<TagInfo> GetTagInfoAsync(
            TagId id,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.TagsGetAsync(id.Value, cancellationToken).ConfigureAwait(false);
                    return new TagInfo(
                        Id: new TagId(response.Uid),
                        Address: response.Address,
                        StartedAt: response.StartedAt,
                        Split: response.Split,
                        Seen: response.Seen,
                        Stored: response.Stored,
                        Sent: response.Sent,
                        Synced: response.Synced);
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<TagInfo[]> GetTagsListAsync(
            int? offset = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var tags =
                        (await beeGeneratedClient.TagsGetAsync(offset, limit, cancellationToken).ConfigureAwait(false)).Tags ??
                        Array.Empty<Clients.Bee.NewTagResponse>();
                    return tags.Select(t => new TagInfo(
                            Id: new TagId(t.Uid),
                            Address: t.Address,
                            StartedAt: t.StartedAt,
                            Split: t.Split,
                            Seen: t.Seen,
                            Stored: t.Stored,
                            Sent: t.Sent,
                            Synced: t.Synced))
                        .ToArray();
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<GnosisChainTx> GetTransactionInfoAsync(
            EthTxHash txHash,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.TransactionsGetAsync(txHash.ToString(), cancellationToken).ConfigureAwait(false);
                    return new GnosisChainTx(
                        transactionHash: response.TransactionHash,
                        to: response.To,
                        nonce: response.Nonce,
                        gasPrice: XDaiValue.FromWeiString(response.GasPrice),
                        gasLimit: response.GasLimit,
                        data: response.Data,
                        created: response.Created,
                        description: response.Description,
                        value: XDaiValue.FromWeiString(response.Value));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<WalletBalances> GetWalletBalance(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.WalletAsync(cancellationToken).ConfigureAwait(false);
                    return new(
                        bzzBalance: BzzValue.FromPlurString(response.BzzBalance),
                        xDaiBalance: XDaiValue.FromWeiString(response.NativeTokenBalance));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<string> GetWelcomeMessageAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.WelcomeMessageGetAsync(cancellationToken).ConfigureAwait(false))
                        .WelcomeMessage1;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public Task<ICollection<string>> GranteeGetAsync(
            string reference,
            RedundancyLevel? redundancyLevel = null,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return beeGeneratedClient.GranteeGetAsync(
                        address: reference,
                        swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel?)redundancyLevel,
                        cancellationToken: cancellationToken);
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<GranteeResponse> GranteePatchAsync(
            string reference,
            string actHistoryAddress,
            PostageBatchId batchId,
            string[] addList,
            string[] revokeList,
            TagId? tagId = null,
            bool? pin = null,
            bool? deferredUpload = null,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return new GranteeResponse(SwarmHash.Zero.ToString(), "");
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.GranteePatchAsync(
                        address: reference,
                        swarm_act_history_address: actHistoryAddress,
                        swarm_postage_batch_id: batchId.ToString(),
                        new ActGranteesPatchRequest { Add = addList, Revoke = revokeList },
                        swarm_tag: tagId?.Value,
                        swarm_pin: pin,
                        swarm_deferred_upload: deferredUpload,
                        swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel)redundancyLevel,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    return new GranteeResponse(response.Ref, response.Historyref);
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<GranteeResponse> GranteePostAsync(
            PostageBatchId batchId,
            string[] grantees,
            TagId? tagId = null,
            bool? pin = null,
            bool? deferredUpload = null,
            string? actHistoryAddress = null,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return new GranteeResponse(SwarmHash.Zero.ToString(), "");

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.GranteePostAsync(
                        swarm_postage_batch_id: batchId.ToString(),
                        new ActGranteesCreateRequest(){ Grantees = grantees },
                        swarm_tag: tagId?.Value,
                        swarm_pin: pin,
                        swarm_deferred_upload: deferredUpload,
                        swarm_act_history_address: actHistoryAddress,
                        swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel)redundancyLevel,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    return new GranteeResponse(response.Ref, response.Historyref);
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<bool> IsChunkExistingAsync(
            SwarmHash hash,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    try
                    {
                        await beeGeneratedClient.ChunksHeadAsync(
                            hash.ToString(),
                            actTimestamp,
                            actPublisher,
                            actHistoryAddress,
                            cancellationToken).ConfigureAwait(false);
                        return true;
                    }
                    catch (SwarmSdkApiException e) when (e.StatusCode == 404)
                    {
                        return false;
                    }
                }
                case SwarmClients.Beehive:
                {
                    try
                    {
                        await beehiveGeneratedClient.ChunksHeadAsync(
                            hash.ToString(),
                            cancellationToken).ConfigureAwait(false);
                        return true;
                    }
                    catch (SwarmSdkApiException e) when (e.StatusCode == 404)
                    {
                        return false;
                    }
                }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<bool> IsContentRetrievableAsync(
            SwarmReference reference,
            RedundancyLevel? redundancyLevel = null,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    return (await beeGeneratedClient.StewardshipGetAsync(
                            address: reference.ToString(),
                            swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel?)redundancyLevel,
                            cancellationToken: cancellationToken).ConfigureAwait(false))
                        .IsRetrievable;
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<LogData> LoggersGetAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.LoggersGetAsync(cancellationToken).ConfigureAwait(false);
                    return new(
                        loggers: response.Loggers.Select(
                            i => new Loggers(
                                id: i.Id,
                                logger: i.Logger1,
                                subsystem: i.Subsystem,
                                verbosity: i.Verbosity)).ToList(),
                        tree: response.Tree.ToDictionary(i => i.Key, i => i.Value?.Plus.ToList() ?? new List<string>()));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<LogData> LoggersGetAsync(string exp, CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.LoggersGetAsync(exp, cancellationToken).ConfigureAwait(false);
                    return new(
                        loggers: response.Loggers.Select(
                            i => new Loggers(
                                id: i.Id,
                                logger: i.Logger1,
                                subsystem: i.Subsystem,
                                verbosity: i.Verbosity)).ToList(),
                        tree: response.Tree.ToDictionary(i => i.Key, i => i.Value?.Plus.ToList() ?? new List<string>()));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task LoggersPutAsync(string exp, Verbosity verbosity, CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return;
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    await beeGeneratedClient.LoggersPutAsync(
                        exp,
                        verbosity switch
                        {
                            Verbosity.None => Etherna.SwarmSdk.Clients.Bee.Verbosity.None,
                            Verbosity.Error => Etherna.SwarmSdk.Clients.Bee.Verbosity.Error,
                            Verbosity.Warning => Etherna.SwarmSdk.Clients.Bee.Verbosity.Warning,
                            Verbosity.Info => Etherna.SwarmSdk.Clients.Bee.Verbosity.Info,
                            Verbosity.Debug => Etherna.SwarmSdk.Clients.Bee.Verbosity.Debug,
                            Verbosity.All => Etherna.SwarmSdk.Clients.Bee.Verbosity.All,
                            _ => throw new ArgumentOutOfRangeException(nameof(verbosity), verbosity, null)
                        },
                        cancellationToken).ConfigureAwait(false);
                    break;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<WebSocket> OpenChunkUploadWebSocketConnectionAsync(
            string endpointPath,
            PostageBatchId batchId,
            TagId? tagId,
            int internalBufferSize,
            int receiveBufferSize,
            int sendBufferSize,
            CancellationToken cancellationToken)
        {
            // Build protocol upgrade request.
            //url
            var urlBuilder = new StringBuilder();
            urlBuilder.Append(NodeUrl);
            urlBuilder.Append(endpointPath);
            var url = urlBuilder.ToString();
            
            //secret key
            byte[] keyBytes = new byte[16];
            RandomNumberGenerator.Fill(keyBytes);
            
            //request
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Connection", "Upgrade");
            request.Headers.Add("Upgrade", "websocket");
            request.Headers.Add("Sec-WebSocket-Version", "13");
            request.Headers.Add("Sec-WebSocket-Key", Convert.ToBase64String(keyBytes));
            request.Headers.Add("swarm-postage-batch-id", batchId.ToString());
            if (tagId.HasValue)
                request.Headers.Add("swarm-tag", tagId.Value.ToString());

            // Send request.
            var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            // Evaluate response and upgrade.
            if (response.StatusCode != HttpStatusCode.SwitchingProtocols)
                throw new InvalidOperationException($"Failed to upgrade to WebSocket: {response.StatusCode}");
            
            // Create websocket from stream.
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var internalBufferArray = new byte[internalBufferSize];
            var webSocket = WebSocket.CreateClientWebSocket(
                stream,
                null,
                receiveBufferSize,
                sendBufferSize,
                WebSocket.DefaultKeepAliveInterval,
                false,
                internalBufferArray);
            return webSocket;
        }

        public async Task<string> RebroadcastTransactionAsync(
            EthTxHash txHash,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return "";

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.TransactionsPostAsync(txHash.ToString(), cancellationToken)
                        .ConfigureAwait(false)).TransactionHash;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<RedistributionState> RedistributionStateAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.RedistributionstateGetAsync(cancellationToken).ConfigureAwait(false);
                    return new(
                        isFrozen: response.IsFrozen,
                        isFullySynced: response.IsFullySynced,
                        isHealthy: response.IsHealthy,
                        enabled: response.Enabled,
                        hasCommittedThisRound: response.HasCommittedThisRound,
                        hasRevealedThisRound: response.HasRevealedThisRound,
                        round: response.Round,
                        lastWonRound: response.LastWonRound,
                        lastPlayedRound: response.LastPlayedRound,
                        lastFrozenRound: response.LastFrozenRound,
                        block: response.Block,
                        reward: BzzValue.FromPlurString(response.Reward),
                        fees: XDaiValue.FromWeiString(response.Fees));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public Task ReuploadContentAsync(
            SwarmReference reference,
            PostageBatchId batchId,
            RedundancyLevel? redundancyLevel = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return Task.CompletedTask;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return beeGeneratedClient.StewardshipPutAsync(
                        address: reference.ToString(),
                        swarm_postage_batch_id: batchId.ToString(),
                        swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel?)redundancyLevel,
                        cancellationToken: cancellationToken);
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public Task SendPssAsync(
            string topic,
            string targets,
            PostageBatchId batchId,
            string? recipient = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return Task.CompletedTask;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return beeGeneratedClient.PssSendAsync(topic, targets, batchId.ToString(), recipient, cancellationToken);
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<bool> SetRedistributionParticipationAsync(
            bool enabled,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return enabled;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.RedistributionstatePatchAsync(
                        new RedistributionEnableRequest { Enabled = enabled },
                        cancellationToken).ConfigureAwait(false);
                    return response.Enabled;
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public Task SetWelcomeMessageAsync(
            string welcomeMessage,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return Task.CompletedTask;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return beeGeneratedClient.WelcomeMessagePostAsync(
                        body: new WelcomeMessage { WelcomeMessage1 = welcomeMessage },
                        cancellationToken: cancellationToken);
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task StakeDeleteAsync(
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    await beeGeneratedClient.StakeDeleteAsync(
                        (long?)gasPrice?.ToWeiDecimal(),
                        gasLimit,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<StakeInfo> StakeGetAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.StakeGetAsync(cancellationToken).ConfigureAwait(false);
                    return new(
                        stakedAmount: BzzValue.FromPlurString(response.StakedAmount),
                        minimumDeposit: response.MinimumDeposit is null ? null : BzzValue.FromPlurString(response.MinimumDeposit));
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task StakePostAsync(
            BzzValue amount,
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    await beeGeneratedClient.StakePostAsync(
                        amount.ToPlurString(),
                        (long?)gasPrice?.ToWeiDecimal(),
                        gasLimit,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public Task StakeWithdrawableDeleteAsync(
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return Task.CompletedTask;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return beeGeneratedClient.StakeWithdrawableDeleteAsync((long?)gasPrice?.ToWeiDecimal(), gasLimit, cancellationToken);
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public Task StakeWithdrawableGetAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return beeGeneratedClient.StakeWithdrawableGetAsync(cancellationToken);
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<StatusNode> StatusNodeAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.StatusAsync(cancellationToken).ConfigureAwait(false);
                    return new(
                        overlay: response.Overlay,
                        beeMode: response.BeeMode switch
                        {
                            Clients.Bee.StatusSnapshotResponseBeeMode.Light => StatusBeeMode.Light,
                            Clients.Bee.StatusSnapshotResponseBeeMode.Full => StatusBeeMode.Full,
                            Clients.Bee.StatusSnapshotResponseBeeMode.UltraLight => StatusBeeMode.UltraLight,
                            Clients.Bee.StatusSnapshotResponseBeeMode.Unknown => StatusBeeMode.Unknown,
                            _ => throw new InvalidOperationException()
                        },
                        proximity: response.Proximity,
                        reserveSize: response.ReserveSize,
                        reserveSizeWithinRadius: response.ReserveSizeWithinRadius,
                        pullsyncRate: response.PullsyncRate,
                        storageRadius: response.StorageRadius,
                        connectedPeers: response.ConnectedPeers,
                        neighborhoodSize: response.NeighborhoodSize,
                        requestFailed: response.RequestFailed,
                        batchCommitment: response.BatchCommitment,
                        isReachable: response.IsReachable,
                        lastSyncedBlock: response.LastSyncedBlock,
                        committedDepth: response.CommittedDepth,
                        isWarmingUp: response.IsWarmingUp);
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<StatusNode[]> StatusPeersAsync(CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                {
                    var response = await beeGeneratedClient.StatusPeersAsync(cancellationToken).ConfigureAwait(false);
                    return response.Snapshots.Select(
                            s => new StatusNode(
                                beeMode: s.BeeMode switch
                                {
                                    Clients.Bee.StatusSnapshotResponseBeeMode.Light => StatusBeeMode.Light,
                                    Clients.Bee.StatusSnapshotResponseBeeMode.Full => StatusBeeMode.Full,
                                    Clients.Bee.StatusSnapshotResponseBeeMode.UltraLight => StatusBeeMode.UltraLight,
                                    Clients.Bee.StatusSnapshotResponseBeeMode.Unknown => StatusBeeMode.Unknown,
                                    _ => throw new InvalidOperationException()
                                },
                                batchCommitment: s.BatchCommitment,
                                connectedPeers: s.ConnectedPeers,
                                isReachable: s.IsReachable,
                                lastSyncedBlock: s.LastSyncedBlock,
                                neighborhoodSize: s.NeighborhoodSize,
                                overlay: s.Overlay,
                                proximity: s.Proximity,
                                pullsyncRate: s.PullsyncRate,
                                reserveSize: s.ReserveSize,
                                reserveSizeWithinRadius: s.ReserveSizeWithinRadius,
                                requestFailed: s.RequestFailed,
                                storageRadius: s.StorageRadius,
                                committedDepth: s.CommittedDepth,
                                isWarmingUp: s.IsWarmingUp))
                        .ToArray();
                }
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public Task SubscribeToGsocAsync(
            string reference,
            IEnumerable<SwarmSocField>? socFields = null,
            bool? cacheWrappedChunk = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return Task.CompletedTask;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return beeGeneratedClient.GsocSubscribeAsync(
                        address: reference,
                        swarm_soc_fieldsHeader: socFields is null ? null : string.Join(',', socFields.Select(f => f switch
                        {
                            SwarmSocField.Address => "address",
                            SwarmSocField.Identifier => "identifier",
                            SwarmSocField.Payload => "payload",
                            SwarmSocField.RecoveredPubKey => "recoveredPubKey",
                            SwarmSocField.Signature => "signature",
                            SwarmSocField.Span => "span",
                            SwarmSocField.WrappedAddress => "wrappedAddress",
                            _ => throw new ArgumentOutOfRangeException(nameof(socFields), f, null)
                        })),
                        swarm_cache_wrapped_chunkHeader: cacheWrappedChunk,
                        cancellationToken: cancellationToken);
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public Task SubscribeToPssAsync(
            string topic,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return Task.CompletedTask;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return beeGeneratedClient.PssSubscribeAsync(topic, cancellationToken);
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<EthTxHash> TopUpPostageBatchAsync(
            PostageBatchId batchId,
            BzzValue amount,
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return EthTxHash.Zero;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.StampsTopupAsync(
                        batch_id: batchId.ToString(),
                        amount: (long)amount.ToPlurDecimal(),
                        gas_limit: gasLimit,
                        gas_price: (long?)gasPrice?.ToWeiDecimal(),
                        cancellationToken: cancellationToken).ConfigureAwait(false)).TxHash;
                case SwarmClients.Beehive:
                    return (await beehiveGeneratedClient.StampsTopupAsync(
                        batchId: batchId.ToString(),
                        amount: amount.ToPlurString(),
                        gas_Limit: gasLimit,
                        gas_Price: gasPrice?.ToWeiString(),
                        cancellationToken: cancellationToken).ConfigureAwait(false)).TxHash;
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<string> TryConnectToPeerAsync(
            string peerId,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return "";

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.PingpongAsync(peerId, cancellationToken).ConfigureAwait(false)).Rtt;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<FileResponse?> TryGetFeedAsync(
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
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    try
                    {
                        var response = await beeGeneratedClient.FeedsGetAsync(
                            owner: owner.ToString(),
                            topic: topic.ToString(),
                            at: at,
                            after: after,
                            type: type.ToString(),
                            swarm_only_root_chunk: onlyRootChunk,
                            swarm_cache: cache,
                            swarm_redundancy_strategy: (Clients.Bee.SwarmRedundancyStrategy?)redundancyStrategy,
                            swarm_redundancy_fallback_mode: redundancyFallbackMode,
                            swarm_chunk_retrieval_timeout: chunkRetrievalTimeout,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                        return new FileResponse(
                            response.ContentHeaders,
                            response.Headers,
                            response.Stream);
                    }
                    catch (SwarmSdkApiException e) when (e.StatusCode == 404)
                    {
                        return null;
                    }
                case SwarmClients.Beehive:
                    try
                    {
                        var response = await beehiveGeneratedClient.FeedsGetAsync(
                            owner: owner.ToString(),
                            topic: topic.ToString(),
                            at: at,
                            after: after,
                            afterLevel: afterLevel,
                            swarm_Only_Root_Chunk: onlyRootChunk,
                            swarm_Redundancy_Strategy: (Clients.Beehive.RedundancyStrategy?)redundancyStrategy,
                            swarm_Redundancy_Fallback_Mode: redundancyFallbackMode,
                            type: (Etherna.SwarmSdk.Clients.Beehive.SwarmFeedType?)type,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                        return new FileResponse(
                            response.ContentHeaders,
                            response.Headers,
                            response.Stream);
                    }
                    catch (SwarmSdkApiException e) when (e.StatusCode == 404)
                    {
                        return null;
                    }
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<HttpContentHeaders?> TryGetFileHeadersAsync(
            SwarmAddress address,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null, 
            RedundancyLevel? redundancyLevel = null,
            RedundancyStrategy? redundancyStrategy = null, 
            bool? redundancyStrategyFallback = null,
            bool? cache = null,
            string? chunkRetrievalTimeout = null,
            CancellationToken cancellationToken = default)
        {
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return address.HasPath ?
                        await beeGeneratedClient.BzzHeadAsync(
                            address: address.Reference.ToString(),
                            path: address.Path,
                            swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel?)redundancyLevel,
                            swarm_redundancy_strategy: (Clients.Bee.SwarmRedundancyStrategy?)redundancyStrategy,
                            swarm_redundancy_fallback_mode: redundancyStrategyFallback,
                            cancellationToken: cancellationToken).ConfigureAwait(false) :
                
                        await beeGeneratedClient.BzzHeadAsync(
                            address: address.Reference.ToString(),
                            swarm_cache: cache,
                            swarm_act_timestamp: actTimestamp,
                            swarm_act_publisher: actPublisher,
                            swarm_act_history_address: actHistoryAddress,
                            swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel?)redundancyLevel,
                            swarm_redundancy_strategy: (Clients.Bee.SwarmRedundancyStrategy?)redundancyStrategy,
                            swarm_redundancy_fallback_mode: redundancyStrategyFallback,
                            swarm_chunk_retrieval_timeout: chunkRetrievalTimeout,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                case SwarmClients.Beehive:
                    return await beehiveGeneratedClient.BzzHeadAsync(
                        address.ToString(),
                        swarm_Redundancy_Level: (Clients.Beehive.RedundancyLevel?)redundancyLevel,
                        swarm_Redundancy_Strategy: (Clients.Beehive.RedundancyStrategy?)redundancyStrategy,
                        swarm_Redundancy_Fallback_Mode: redundancyStrategyFallback,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<long?> TryGetFileSizeAsync(
            SwarmAddress address,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null, 
            RedundancyLevel? redundancyLevel = null,
            RedundancyStrategy? redundancyStrategy = null, 
            bool? redundancyStrategyFallback = null,
            bool? cache = null,
            string? chunkRetrievalTimeout = null,
            CancellationToken cancellationToken = default)
        {
            var headers = await TryGetFileHeadersAsync(
                address: address,
                actTimestamp: actTimestamp,
                actPublisher: actPublisher,
                actHistoryAddress: actHistoryAddress,
                redundancyLevel: redundancyLevel,
                redundancyStrategy: redundancyStrategy,
                redundancyStrategyFallback: redundancyStrategyFallback,
                cache: cache,
                chunkRetrievalTimeout: chunkRetrievalTimeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return headers?.ContentLength;
        }

        public async Task UpdatePostageBatchLabelAsync(
            PostageBatchId batchId,
            string label,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    await beeGeneratedClient.StampsPatchAsync(
                        new Body { Label = label },
                        batchId.ToString(),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public Task UpdateTagAsync(
            TagId id,
            SwarmHash? hash = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return Task.CompletedTask;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return beeGeneratedClient.TagsPatchAsync(
                        id: id.Value,
                        body: hash.HasValue ? new Address { Address1 = hash.Value.ToString() } : null,
                        cancellationToken: cancellationToken);
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<SwarmReference> UploadBytesAsync(
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
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(body);

            if (IsDryMode)
                return encrypt == true ? SwarmReference.EncryptedZero : SwarmReference.PlainZero;

            // Retrying needs to resend the same data, so a non-seekable stream cannot be rewound.
            if (maxUploadAttempts > 1 && !body.CanSeek)
                throw new ArgumentException(
                    "A seekable stream is required to retry the upload; set maxUploadAttempts to 1 for non-seekable streams.",
                    nameof(body));
            var startPosition = body.CanSeek ? body.Position : 0L;

            return await UploadRetrier.ExecuteWithRetryAsync(
                async ct =>
                {
                    if (body.CanSeek)
                        body.Position = startPosition;
                    switch (ApiCompatibility)
                    {
                        case SwarmClients.Bee:
                            return (await beeGeneratedClient.BytesPostAsync(
                                swarm_postage_batch_id: batchId.ToString(),
                                swarm_tag: tagId?.Value,
                                swarm_pin: pin,
                                swarm_deferred_upload: deferredUpload,
                                swarm_encrypt: encrypt,
                                swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel)redundancyLevel,
                                swarm_act: act,
                                swarm_act_history_address: actHistoryAddress,
                                body: body,
                                cancellationToken: ct).ConfigureAwait(false)).Reference;
                        case SwarmClients.Beehive:
                            return (await beehiveGeneratedClient.BytesPostAsync(
                                swarm_Postage_Batch_Id: batchId.ToString(),
                                body: body,
                                swarm_Compact_Level: compactLevel,
                                swarm_Encrypt: encrypt,
                                swarm_Pin: pin,
                                swarm_Redundancy_Level: (Clients.Beehive.RedundancyLevel?)redundancyLevel,
                                cancellationToken: ct).ConfigureAwait(false)).Reference;
                        default:
                            throw new InvalidOperationException();
                    }
                },
                maxAttempts: maxUploadAttempts,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public Task<SwarmHash> UploadChunkAsync(
            SwarmCac chunk,
            PostageBatchId? batchId,
            TagId? tagId = null,
            PostageStamp? presignedPostageStamp = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(chunk);
            
#pragma warning disable CA2025
            using var memoryStream = new MemoryStream(chunk.GetFullPayloadToByteArray());
            return UploadChunkAsync(
                memoryStream,
                batchId,
                tagId,
                presignedPostageStamp,
                cancellationToken);
#pragma warning restore CA2025
        }

        public async Task<SwarmHash> UploadChunkAsync(
            Stream chunkData,
            PostageBatchId? batchId,
            TagId? tagId = null,
            PostageStamp? presignedPostageStamp = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return SwarmHash.Zero;

            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.ChunksPostAsync(
                        swarm_tag: tagId?.Value,
                        swarm_postage_batch_id: batchId?.ToString(),
                        swarm_postage_stamp: presignedPostageStamp?.ToString(),
                        body: chunkData,
                        cancellationToken: cancellationToken).ConfigureAwait(false)).Reference;
                case SwarmClients.Beehive:
                    return (await beehiveGeneratedClient.ChunksPostAsync(
                        body: chunkData,
                        swarm_Postage_Batch_Id: batchId?.ToString(),
                        swarm_Postage_Stamp: presignedPostageStamp?.ToString(),
                        cancellationToken: cancellationToken).ConfigureAwait(false)).Reference;
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<SwarmReference> UploadDirectoryAsync(
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
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return encrypt == true ? SwarmReference.EncryptedZero : SwarmReference.PlainZero;

            // Create tar file once; the seekable stream is rewound before each attempt.
            using var memoryStream = new MemoryStream();
            await TarFile.CreateFromDirectoryAsync(directoryPath, memoryStream, false, cancellationToken).ConfigureAwait(false);

            // Try set index document.
            if (indexDocument is null &&
                File.Exists(Path.Combine(directoryPath, "index.html")))
                indexDocument = "index.html";

            // Upload directory.
            return await UploadRetrier.ExecuteWithRetryAsync(
                async ct =>
                {
                    memoryStream.Position = 0;
                    switch (ApiCompatibility)
                    {
                        case SwarmClients.Bee:
                            return (await beeGeneratedClient.BzzPostAsync(
                                name: null,
                                swarm_tag: tagId?.Value,
                                swarm_pin: pin,
                                swarm_encrypt: encrypt,
                                content_Type: "application/x-tar",
                                swarm_collection: true,
                                swarm_index_document: indexDocument,
                                swarm_error_document: errorDocument,
                                swarm_postage_batch_id: batchId.ToString(),
                                swarm_deferred_upload: deferredUpload,
                                swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel)redundancyLevel,
                                swarm_act: act,
                                swarm_act_history_address: actHistoryAddress,
                                body: memoryStream,
                                cancellationToken: ct).ConfigureAwait(false)).Reference;
                        case SwarmClients.Beehive:
                            return (await beehiveGeneratedClient.BzzPostAsync(
                                body: new Clients.Beehive.FileParameter(
                                    data: memoryStream,
                                    fileName: null,
                                    contentType: "application/x-tar"),
                                swarm_Postage_Batch_Id: batchId.ToString(),
                                swarm_Compact_Level: compactLevel,
                                swarm_Encrypt: encrypt,
                                swarm_Pin: pin,
                                swarm_Redundancy_Level: (Clients.Beehive.RedundancyLevel)redundancyLevel,
                                swarm_Collection: true,
                                swarm_Index_Document: indexDocument,
                                swarm_Error_Document: errorDocument,
                                cancellationToken: ct).ConfigureAwait(false)).Reference;
                        default:
                            throw new InvalidOperationException();
                    }
                },
                maxAttempts: maxUploadAttempts,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<SwarmHash> UpdateFeedAsync(
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
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(signer);

            // The feed owner is the signer's address.
            var owner = signer.PublicAddress;
            SwarmFeedBase feed = type switch
            {
                SwarmFeedType.Epoch => new SwarmEpochFeed(owner, topic),
                SwarmFeedType.Sequence => new SwarmSequenceFeed(owner, topic),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            // Resolve the next index against the node, build the update with the given data
            // (passed to the feed as-is) and sign it as a SOC.
            var chunkBmt = new SwarmChunkBmt();
            var feedChunk = await feed.BuildNextFeedChunkAsync(
                data,
                knownNearIndex,
                new SwarmClientChunkStore(this),
                chunkBmt,
                timestamp).ConfigureAwait(false);
            feedChunk.Sign(signer, chunkBmt.Hasher);

            return await UploadSocAsync(
                feedChunk,
                batchId,
                tagId: tagId,
                deferredUpload: deferredUpload,
                pin: pin,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<SwarmHash> UploadFeedManifestAsync(
            SwarmFeedBase feed,
            PostageBatchId batchId,
            ushort? compactLevel = 0,
            bool pin = false,
            bool? act = null,
            string? actHistoryAddress = null,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(feed);
            
            if (IsDryMode)
                return SwarmHash.Zero;
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.FeedsPostAsync(
                        owner: feed.Owner.ToString(),
                        topic: feed.Topic.ToString(),
                        type: feed.Type.ToString(),
                        swarm_pin: pin,
                        swarm_postage_batch_id: batchId.ToString(),
                        swarm_act: act,
                        swarm_act_history_address: actHistoryAddress,
                        swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel)redundancyLevel,
                        cancellationToken: cancellationToken).ConfigureAwait(false)).Reference;
                case SwarmClients.Beehive:
                    return (await beehiveGeneratedClient.FeedsPostAsync(
                        owner: feed.Owner.ToString(),
                        topic: feed.Topic.ToString(),
                        swarm_Postage_Batch_Id: batchId.ToString(),
                        swarm_Compact_Level: compactLevel,
                        swarm_Pin: pin,
                        type: (Clients.Beehive.SwarmFeedType?)feed.Type,
                        cancellationToken: cancellationToken).ConfigureAwait(false)).Reference;
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<SwarmReference> UploadFileAsync(
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
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);

            if (IsDryMode)
                return encrypt == true ? SwarmReference.EncryptedZero : SwarmReference.PlainZero;

            // Retrying needs to resend the same data, so a non-seekable stream cannot be rewound.
            if (maxUploadAttempts > 1 && !content.CanSeek)
                throw new ArgumentException(
                    "A seekable stream is required to retry the upload; set maxUploadAttempts to 1 for non-seekable streams.",
                    nameof(content));
            var startPosition = content.CanSeek ? content.Position : 0L;

            return await UploadRetrier.ExecuteWithRetryAsync(
                async ct =>
                {
                    if (content.CanSeek)
                        content.Position = startPosition;
                    switch (ApiCompatibility)
                    {
                        case SwarmClients.Bee:
                            return (await beeGeneratedClient.BzzPostAsync(
                                name: name,
                                swarm_tag: tagId?.Value,
                                swarm_pin: pin,
                                swarm_encrypt: encrypt,
                                content_Type: contentType,
                                swarm_collection: isFileCollection,
                                swarm_index_document: indexDocument,
                                swarm_error_document: errorDocument,
                                swarm_postage_batch_id: batchId.ToString(),
                                swarm_deferred_upload: deferredUpload,
                                swarm_redundancy_level: (Clients.Bee.SwarmRedundancyLevel)redundancyLevel,
                                body: content,
                                cancellationToken: ct).ConfigureAwait(false)).Reference;
                        case SwarmClients.Beehive:
                            return (await beehiveGeneratedClient.BzzPostAsync(
                                body: new Clients.Beehive.FileParameter(
                                    data: content,
                                    fileName: name,
                                    contentType: contentType),
                                swarm_Postage_Batch_Id: batchId.ToString(),
                                swarm_Compact_Level: compactLevel,
                                swarm_Encrypt: encrypt,
                                swarm_Pin: pin,
                                swarm_Redundancy_Level: (Clients.Beehive.RedundancyLevel)redundancyLevel,
                                swarm_Collection: isFileCollection,
                                swarm_Index_Document: indexDocument,
                                swarm_Error_Document: errorDocument,
                                cancellationToken: ct).ConfigureAwait(false)).Reference;
                        default:
                            throw new InvalidOperationException();
                    }
                },
                maxAttempts: maxUploadAttempts,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<SwarmHash> UploadSocAsync(
            SwarmSoc soc,
            PostageBatchId? batchId,
            PostageStamp? presignedPostageStamp = null,
            TagId? tagId = null,
            bool deferredUpload = false,
            bool? pin = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(soc);
            if (!soc.Signature.HasValue)
                throw new InvalidOperationException("SOC is not signed");
            
            if (IsDryMode)
                return SwarmHash.Zero;

            using var bodyMemoryStream = new MemoryStream(soc.InnerChunk.SpanData.ToArray());
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.SocPostAsync(
                        owner: soc.Owner.ToString(false),
                        id: soc.Identifier.ToString(),
                        sig: soc.Signature.Value.ToString(),
                        body: bodyMemoryStream,
                        swarm_postage_batch_id: batchId?.ToString(),
                        swarm_postage_stamp: presignedPostageStamp?.ToString(),
                        swarm_tag: tagId?.Value,
                        swarm_pin: pin,
                        swarm_deferred_upload: deferredUpload,
                        cancellationToken: cancellationToken).ConfigureAwait(false)).Reference;
                case SwarmClients.Beehive:
                    return (await beehiveGeneratedClient.SocPostAsync(
                        owner: soc.Owner.ToString(false),
                        id: soc.Identifier.ToString(),
                        sig: soc.Signature.Value.ToString(),
                        body: bodyMemoryStream,
                        swarm_Postage_Batch_Id: batchId?.ToString(),
                        swarm_Postage_Stamp: presignedPostageStamp?.ToString(),
                        swarm_Pin: pin,
                        cancellationToken: cancellationToken).ConfigureAwait(false)).Reference;
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<EthTxHash> WalletBzzWithdrawAsync(
            BzzValue amount,
            EthAddress address,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return EthTxHash.Zero;
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.WalletWithdrawAsync(
                        amount.ToPlurString(),
                        address.ToString(),
                        Clients.Bee.WithdrawCoin.Bzz,
                        cancellationToken).ConfigureAwait(false)).TransactionHash;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<EthTxHash> WalletNativeCoinWithdrawAsync(
            XDaiValue amount,
            EthAddress address,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return EthTxHash.Zero;
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.WalletWithdrawAsync(
                        amount.ToWeiString(),
                        address.ToString(),
                        Clients.Bee.WithdrawCoin.Nativetoken,
                        cancellationToken).ConfigureAwait(false)).TransactionHash;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public async Task<EthTxHash> WithdrawFromChequebookAsync(
            BzzValue amount,
            XDaiValue? gasPrice = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryMode)
                return EthTxHash.Zero;
            
            switch (ApiCompatibility)
            {
                case SwarmClients.Bee:
                    return (await beeGeneratedClient.ChequebookWithdrawAsync(
                        (long)amount.ToPlurDecimal(),
                        (long?)gasPrice?.ToWeiDecimal(),
                        cancellationToken).ConfigureAwait(false)).TransactionHash;
                case SwarmClients.Beehive:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        // Helpers.
        private static BzzValue ReadChainPrice(string plurValue)
        {
            //the node reports a big-int string, which can exceed long; a price is forced >= 1 plur
            var price = BzzValue.FromPlurString(plurValue);
            return price < MinChainPrice ? MinChainPrice : price;
        }
    }
}