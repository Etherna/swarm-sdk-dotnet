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
        // Consts.
        private const int HeaderSize = EncryptionKey256.KeySize + VersionHashSize;

        /// <summary>
        /// The size no node can exceed: its header, and a fork for each of the 256 keys with the longest reference
        /// and the longest metadata the format can declare. A content declaring more is not read as a node
        /// </summary>
        private const int MaxNodeSize =
            EncryptionKey256.KeySize + VersionHashSize + 1 + byte.MaxValue + ForksIndexSize +
            ForksIndexSize * 8 * (
                MantarayNodeFork.HeaderSize + MantarayNodeFork.PrefixMaxSize + byte.MaxValue +
                MantarayNodeFork.MetadataBytesSize + ushort.MaxValue + EncryptionKey256.KeySize);

        // Fields.
        private SwarmReference? _entryReference;
        private readonly Dictionary<char, MantarayNodeFork> _forks = new();
        private readonly IReadOnlyDictionary<string, string> _metadata;
        private EncryptionKey256? _obfuscationKey;
        private readonly IReadOnlyChunkStore chunkStore;
        private readonly RedundancyLevel redundancyLevel;
        private readonly RedundancyStrategy redundancyStrategy;
        private readonly bool redundancyStrategyFallback;

        // Constructors.
        public ReferencedMantarayNode(
            SwarmCac chunk,
            SwarmReference reference,
            IReadOnlyChunkStore chunkStore,
            RedundancyLevel redundancyLevel,
            RedundancyStrategy redundancyStrategy,
            bool redundancyStrategyFallback,
            IReadOnlyDictionary<string, string>? metadata,
            NodeType nodeTypeFlags)
        {
            ArgumentNullException.ThrowIfNull(chunk);
            
            if (chunk.Hash != reference.Hash)
                throw new SwarmChunkVerificationException(
                    reference.Hash,
                    $"Chunk's hash {chunk.Hash} does not match reference {reference}");

            this.chunkStore = chunkStore;
            this.redundancyLevel = redundancyLevel;
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
            RedundancyLevel redundancyLevel,
            RedundancyStrategy redundancyStrategy,
            bool redundancyStrategyFallback,
            IReadOnlyDictionary<string, string>? metadata,
            NodeType nodeTypeFlags)
        {
            this.chunkStore = chunkStore;
            this.redundancyLevel = redundancyLevel;
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
        /// <summary>
        /// Decode the node from the content of its chunk tree: a node with enough forks spans more chunks than its
        /// root, which are fetched with the redundancy strategy of the node
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <exception cref="InvalidOperationException">The content at the reference is not a manifest node</exception>
        public async Task DecodeFromChunkAsync(CancellationToken cancellationToken = default)
        {
            if (Chunk == null)
                throw new InvalidOperationException("Chunk not fetched");
            if (IsDecoded)
                return;
            
            var hasher = new Hasher();
            var decodedRoot = Chunk.Decode(Reference, hasher);
            if (decodedRoot.SpanLength < HeaderSize)
                throw new InvalidOperationException("Content is too short to be a manifest node");
            if (decodedRoot.SpanLength > MaxNodeSize)
                throw new InvalidOperationException("Content is larger than a manifest node can be");

            // Read the content of the node: of its only chunk, or joined from its chunk tree.
            byte[] data;
            if (decodedRoot is SwarmDecodedDataCac dataRoot)
                data = dataRoot.Data.ToArray();
            else
            {
                // A chunk tree can be a node, or a raw content the traverser is probing: its first bytes tell which,
                // the obfuscation key and the version hash it obfuscates. They open the first data chunk of the tree:
                // reach it through the first child of each intermediate chunk, one fetch for each level, and refuse a
                // raw content there, without joining it.
                var leftmostChunk = decodedRoot;
                while (leftmostChunk is SwarmDecodedIntermediateCac intermediateChunk)
                {
                    var childReference = intermediateChunk.ChildReferences[0].Reference;
                    if (await chunkStore.TryGetAsync(childReference.Hash, cancellationToken).ConfigureAwait(false)
                        is not SwarmCac childChunk)
                        break; //the store misses it: the join can recover it from the parities

                    var decodedChild = childChunk.Decode(childReference, hasher);
                    if (decodedChild.SpanLength != intermediateChunk.DataReferenceSpanLengths[0])
                        break; //a malformed tree, that the join rejects
                    leftmostChunk = decodedChild;
                }
                if (leftmostChunk is SwarmDecodedDataCac leftmostDataChunk)
                    ThrowIfNotNodeHeader(leftmostDataChunk.Data);

                var stream = ChunkDataStream.BuildNew(
                    Chunk,
                    Reference,
                    chunkStore,
                    redundancyStrategy,
                    redundancyStrategyFallback);
                await using (stream.ConfigureAwait(false))
                {
                    data = new byte[stream.Length];
                    await stream.ReadExactlyAsync(data, cancellationToken).ConfigureAwait(false);
                }
            }

            // Whatever path read it, a content is decoded as a node only with the header of one.
            ThrowIfNotNodeHeader(data);

            // De-obfuscate and decode.
            var obfuscationKey = new EncryptionKey256(data.AsMemory()[..EncryptionKey256.KeySize]);
            obfuscationKey.XorEncryptDecrypt(data.AsSpan()[EncryptionKey256.KeySize..]);
            DecodeVersion02(data.AsMemory()[HeaderSize..]);
            _obfuscationKey = obfuscationKey;

            IsDecoded = true;
        }
        
        /// <summary>
        /// Fetch the chunk of the node, looking up its replicas at the redundancy level of the node
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        public async Task FetchChunkAsync(CancellationToken cancellationToken = default)
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
                await FetchChunkAsync(cancellationToken).ConfigureAwait(false);
            
            if (!IsDecoded)
                await DecodeFromChunkAsync(cancellationToken).ConfigureAwait(false);
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
                    redundancyLevel,
                    redundancyStrategy,
                    redundancyStrategyFallback,
                    childNodeMetadata,
                    childNodeTypeFlags);
                _forks[key] = new MantarayNodeFork(prefix, forkNode);
            }
        }

        /// <summary>
        /// Tell a manifest node from any other content on its first bytes: the obfuscation key, and the version hash
        /// it obfuscates
        /// </summary>
        private static void ThrowIfNotNodeHeader(ReadOnlyMemory<byte> content)
        {
            var obfuscationKey = new EncryptionKey256(content[..EncryptionKey256.KeySize]);
            var versionHash = content[EncryptionKey256.KeySize..HeaderSize].ToArray();
            obfuscationKey.XorEncryptDecrypt(versionHash);
            if (!versionHash.AsSpan().SequenceEqual(Version02Hash))
                throw new InvalidOperationException("Manifest version not recognized");
        }
    }
}