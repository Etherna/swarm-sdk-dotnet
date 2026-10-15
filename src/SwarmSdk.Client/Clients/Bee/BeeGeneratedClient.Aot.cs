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

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Etherna.SwarmSdk.Clients.Bee
{
    // Native AOT support for the NSwag-generated BeeGeneratedClient.
    //
    // NSwag does not (yet) emit a System.Text.Json source-generation context, so the generated
    // client would fall back to reflection-based serialization, which is not trim/AOT safe.
    // This source-generated context provides JsonTypeInfo metadata for every request/response
    // DTO that flows through the client, and it is wired into the client's JsonSerializerOptions
    // via the UpdateJsonSerializerSettings partial hook below. The generated (de)serialization
    // calls have been switched to the JsonTypeInfo-based JsonSerializer overloads, so they resolve
    // through this context with no reflection or dynamic code.
    //
    // Only the "root" types (request bodies and operation response types) are listed: the source
    // generator pulls in nested DTOs and enums transitively. When the OpenAPI client is
    // regenerated this list must be kept in sync (or dropped entirely once NSwag supports AOT).
    [JsonSerializable(typeof(ActGranteesCreateRequest))]
    [JsonSerializable(typeof(ActGranteesPatchRequest))]
    [JsonSerializable(typeof(ActGranteesOperationResponse))]
    [JsonSerializable(typeof(Address))]
    [JsonSerializable(typeof(Addresses))]
    [JsonSerializable(typeof(ApiRCHashResponse))]
    [JsonSerializable(typeof(Balance))]
    [JsonSerializable(typeof(Balances))]
    [JsonSerializable(typeof(BatchIDResponse))]
    [JsonSerializable(typeof(BlockListedPeers))]
    [JsonSerializable(typeof(Body))]
    [JsonSerializable(typeof(BzzTopology))]
    [JsonSerializable(typeof(ChainState))]
    [JsonSerializable(typeof(ChequeAllPeersResponse))]
    [JsonSerializable(typeof(ChequebookAddress))]
    [JsonSerializable(typeof(ChequebookBalance))]
    [JsonSerializable(typeof(ChequePeerResponse))]
    [JsonSerializable(typeof(DebugPostageAllBatchesResponse))]
    [JsonSerializable(typeof(DebugPostageBatchesResponse))]
    [JsonSerializable(typeof(GetStakeResponse))]
    [JsonSerializable(typeof(GetWithdrawableResponse))]
    [JsonSerializable(typeof(HealthStatus))]
    [JsonSerializable(typeof(IsRetrievableResponse))]
    [JsonSerializable(typeof(LoggerResponse))]
    [JsonSerializable(typeof(NewTagResponse))]
    [JsonSerializable(typeof(Node))]
    [JsonSerializable(typeof(PeerAccountingData))]
    [JsonSerializable(typeof(Peers))]
    [JsonSerializable(typeof(PendingTransactionsResponse))]
    [JsonSerializable(typeof(PinCheckResponse))]
    [JsonSerializable(typeof(PostageBatch))]
    [JsonSerializable(typeof(PostageBatchShort))]
    [JsonSerializable(typeof(PostageStampBuckets))]
    [JsonSerializable(typeof(PostEnvelopeResponse))]
    [JsonSerializable(typeof(ProblemDetails))]
    [JsonSerializable(typeof(RedistributionEnableRequest))]
    [JsonSerializable(typeof(RedistributionEnableResponse))]
    [JsonSerializable(typeof(RedistributionStatusResponse))]
    [JsonSerializable(typeof(ReferenceResponse))]
    [JsonSerializable(typeof(ReserveState))]
    [JsonSerializable(typeof(Response))]
    [JsonSerializable(typeof(Response2))]
    [JsonSerializable(typeof(RttMs))]
    [JsonSerializable(typeof(Settlement))]
    [JsonSerializable(typeof(Settlements))]
    [JsonSerializable(typeof(StakeDepositErrorResponse))]
    [JsonSerializable(typeof(StakeTransactionResponse))]
    [JsonSerializable(typeof(StatusNeighborhoodsResponse))]
    [JsonSerializable(typeof(StatusPeersResponse))]
    [JsonSerializable(typeof(StatusSnapshotResponse))]
    [JsonSerializable(typeof(SwapCashoutStatus))]
    [JsonSerializable(typeof(SwarmOnlyReferenceResponse))]
    [JsonSerializable(typeof(SwarmOnlyReferencesList))]
    [JsonSerializable(typeof(TagsList))]
    [JsonSerializable(typeof(TransactionInfo))]
    [JsonSerializable(typeof(TransactionResponse))]
    [JsonSerializable(typeof(WalletResponse))]
    [JsonSerializable(typeof(WalletTxResponse))]
    [JsonSerializable(typeof(WelcomeMessage))]
    [JsonSerializable(typeof(ICollection<string>))]
    internal sealed partial class BeeJsonSerializerContext : JsonSerializerContext;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1852:Seal internal types",
        Justification = "NSwag-generated client is intentionally unsealed (virtual members / partial extension point).")]
    internal partial class BeeGeneratedClient
    {
        static partial void UpdateJsonSerializerSettings(System.Text.Json.JsonSerializerOptions settings) =>
            settings.TypeInfoResolverChain.Add(BeeJsonSerializerContext.Default);

        // Helpers.
        // Serializes a request body through the source-generated context (AOT-safe). Resolves the
        // JsonTypeInfo from the body's static type T, so a null body serializes as "null" exactly like
        // the original generated SerializeToUtf8Bytes<T>(body, options) call did.
        private byte[] SerializeBody<T>(T body) =>
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                body, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)JsonSerializerSettings.GetTypeInfo(typeof(T)));
    }
}
