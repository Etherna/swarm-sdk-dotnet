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
using Etherna.SwarmSdk.Extensions;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Manifest
{
    public sealed class ReferencedMantarayNode : MantarayNodeBase
    {
        // Fields.
        private SwarmReference? _entryReference;
        private readonly Dictionary<char, MantarayNodeFork> _forks = new();
        private readonly IReadOnlyDictionary<string, string> _metadata;
        private EncryptionKey256? _obfuscationKey;
        private readonly IReadOnlyChunkStore chunkStore;
        private readonly RedundancyStrategy redundancyStrategy;
        private readonly bool redundancyStrategyFallback;

        // Constructors.
        public ReferencedMantarayNode(
            SwarmCac chunk,
            SwarmReference reference,
            IReadOnlyChunkStore chunkStore,
            RedundancyStrategy redundancyStrategy,
            bool redundancyStrategyFallback,
            IReadOnlyDictionary<string, string>? metadata,
            NodeType nodeTypeFlags)
        {
            ArgumentNullException.ThrowIfNull(chunk);
            
            if (chunk.Hash != reference.Hash)
                throw new ArgumentException($"Chunk's hash {chunk.Hash} does not match reference {reference}");

            this.chunkStore = chunkStore;
            this.redundancyStrategy = redundancyStrategy;
            this.redundancyStrategyFallback = redundancyStrategyFallback;
            _metadata = metadata ?? new Dictionary<string, string>();
            Chunk = chunk;
            NodeTypeFlags = nodeTypeFlags;
            Reference = reference;
        }
        
        public ReferencedMantarayNode(
            SwarmReference reference,
            IReadOnlyChunkStore chunkStore,
            RedundancyStrategy redundancyStrategy,
            bool redundancyStrategyFallback,
            IReadOnlyDictionary<string, string>? metadata,
            NodeType nodeTypeFlags)
        {
            this.chunkStore = chunkStore;
            this.redundancyStrategy = redundancyStrategy;
            this.redundancyStrategyFallback = redundancyStrategyFallback;
            _metadata = metadata ?? new Dictionary<string, string>();
            NodeTypeFlags = nodeTypeFlags;
            Reference = reference;
        }

        // Properties.
        public SwarmCac? Chunk { get; private set; }
        public override SwarmReference? EntryReference => IsDecoded
            ? _entryReference
            : throw new InvalidOperationException("Node is not decoded from chunk");
        public override IReadOnlyDictionary<char, MantarayNodeFork> Forks => IsDecoded
            ? _forks
            : throw new InvalidOperationException("Node is not decoded from chunk");
        public bool IsDecoded { get; private set; }
        public override IReadOnlyDictionary<string, string> Metadata => _metadata;
        public override NodeType NodeTypeFlags { get; }
        public override EncryptionKey256? ObfuscationKey => IsDecoded
            ? _obfuscationKey
            : throw new InvalidOperationException("Node is not decoded from chunk");
        public override SwarmReference Reference { get; }

        // Methods.
        public void DecodeFromChunk()
        {
            if (Chunk == null)
                throw new InvalidOperationException("Chunk not fetched");
            if (IsDecoded)
                return;
            
            // Get chunk data.
            if (Chunk.Decode(Reference, new Hasher()) is not SwarmDecodedDataCac decodedChunk)
                throw new InvalidOperationException("Intermediate chunk is not a manifest node");
            var data = decodedChunk.Data.ToArray();
            if (data.Length < EncryptionKey256.KeySize + VersionHashSize)
                throw new InvalidOperationException("Chunk is too short to be a manifest node");

            // Get obfuscation key and de-obfuscate.
            var readIndex = 0;
            _obfuscationKey = new EncryptionKey256(data.AsMemory()[..EncryptionKey256.KeySize]);
            _obfuscationKey.Value.XorEncryptDecrypt(data.AsSpan()[EncryptionKey256.KeySize..]);
            readIndex += EncryptionKey256.KeySize;
            
            // Read header.
            var versionHash = data.AsMemory()[readIndex..(readIndex + VersionHashSize)];
            readIndex += VersionHashSize;
            
            // Decode version.
            if (versionHash.Span.SequenceEqual(Version02Hash))
                DecodeVersion02(data.AsMemory()[readIndex..]);
            else
                throw new InvalidOperationException("Manifest version not recognized");

            IsDecoded = true;
        }
        
        public async Task FetchChunkAsync(
            RedundancyLevel redundancyLevel,
            CancellationToken cancellationToken = default)
        {
            // Use chunk replica resolver if required.
            var rootChunkStore = redundancyLevel == RedundancyLevel.None ?
                chunkStore :
                new ReplicaResolverChunkStore(chunkStore, redundancyLevel, new Hasher());
            
            // Resolve root chunk.
            var rootChunk = await rootChunkStore.GetAsync(
                Reference.Hash,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (rootChunk is not SwarmCac rootCac) //soc is not supported
                throw new SwarmChunkTypeException(rootChunk, $"Chunk {Reference} is not a Content Addressed Chunk.");

            Chunk = rootCac;
        }
        
        public override async Task OnVisitingAsync(CancellationToken cancellationToken = default)
        {
            if (Chunk is null)
                await FetchChunkAsync(RedundancyLevel.Paranoid, cancellationToken).ConfigureAwait(false);
            
            if (!IsDecoded)
                DecodeFromChunk();
        }

        // Helpers.
        private void DecodeVersion02(ReadOnlyMemory<byte> data)
        {
            var readIndex = 0;
            
            // Read last entry hash.
            var entryReferenceSize = data.Span[readIndex];
            readIndex++;
            
            if (entryReferenceSize != 0)
            {
                _entryReference = new SwarmReference(data[readIndex..(readIndex + entryReferenceSize)]);
                readIndex += entryReferenceSize;
            }
            
            // Read forks.
            //index
            var forksIndex = data[readIndex..(readIndex + ForksIndexSize)];
            readIndex += ForksIndexSize;
            
            var forksKeys = new List<char>();
            for (int i = 0; i < ForksIndexSize * 8; i++)
            {
                if ((forksIndex.Span[i / 8] & (byte)(1 << (i % 8))) != 0)
                    forksKeys.Add((char)i);
            }
            
            //forks
            foreach (var key in forksKeys)
            {
                var childNodeTypeFlags = (NodeType)data.Span[readIndex++];
                var prefixLength = data.Span[readIndex++];
                
                //read prefix
                var prefix = Encoding.UTF8.GetString(data.Span[readIndex..(readIndex + MantarayNodeFork.PrefixMaxSize)])[..prefixLength];
                readIndex += MantarayNodeFork.PrefixMaxSize;

                //read child node reference
                var childNodeReference = new SwarmReference(data[readIndex..(readIndex + entryReferenceSize)]);
                readIndex += entryReferenceSize;
                
                //read metadata
                Dictionary<string, string>? childNodeMetadata = null;
                if (childNodeTypeFlags.HasFlag(NodeType.WithMetadata))
                {
                    var metadataBytesLength = BinaryPrimitives.ReadUInt16BigEndian(
                        data.Span[readIndex..(readIndex + MantarayNodeFork.MetadataBytesSize)]);
                    readIndex += MantarayNodeFork.MetadataBytesSize;

                    var metadataBytes = data[readIndex..(readIndex + metadataBytesLength)];
                    readIndex += metadataBytesLength;

                    childNodeMetadata = JsonSerializer.Deserialize(
                        Encoding.UTF8.GetString(metadataBytes.Span),
                        ManifestJsonSerializerContext.Default.DictionaryStringString);
                    
                    //skip padding
                    var metadataTotalSize = metadataBytes.Length + MantarayNodeFork.MetadataBytesSize;
                    if (metadataTotalSize % EncryptionKey256.KeySize != 0)
                        readIndex += EncryptionKey256.KeySize - metadataTotalSize % EncryptionKey256.KeySize;
                }
                
                //add fork
                var forkNode = new ReferencedMantarayNode(
                    childNodeReference,
                    chunkStore,
                    redundancyStrategy,
                    redundancyStrategyFallback,
                    childNodeMetadata,
                    childNodeTypeFlags);
                _forks[key] = new MantarayNodeFork(prefix, forkNode);
            }
        }
    }
}