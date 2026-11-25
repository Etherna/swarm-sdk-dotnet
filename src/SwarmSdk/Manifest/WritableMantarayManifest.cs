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

using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Manifest
{
    public delegate IHasherPipeline BuildHasherPipeline(bool readOnlyPipeline);
    
    public sealed class WritableMantarayManifest : MantarayManifestBase
    {
        // Fields.
        private readonly BuildHasherPipeline hasherPipelineBuilder;
        private readonly WritableMantarayNode rootNode;

        // Constructors.
        public WritableMantarayManifest(
            BuildHasherPipeline hasherPipelineBuilder,
            WritableMantarayNode rootNode)
        {
            this.hasherPipelineBuilder = hasherPipelineBuilder;
            this.rootNode = rootNode;
        }
        
        public WritableMantarayManifest(
            IChunkStore chunkStore,
            IPostageStamper postageStamper,
            RedundancyLevel redundancyLevel,
            bool encrypt,
            ushort compactLevel,
            int? chunkHashingConcurrency)
        {
            /*
             * If encrypted:
             * - compact chunks and encrypt with the hashing pipeline
             * - use obfuscation with random keys (initialize key == null). Don't mine on obfuscation
             *
             * If not encrypted, but compacted:
             * - compact chunks when required using deterministic obfuscation keys
             *
             * If not encrypted and not compacted:
             * - set obfuscation key to EncryptionKey256.Zero
             */

            hasherPipelineBuilder = readOnlyPipeline => HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStore,
                postageStamper,
                redundancyLevel,
                encrypt,
                encrypt ? compactLevel : (ushort)0,
                chunkHashingConcurrency,
                readOnlyPipeline);
            rootNode = new WritableMantarayNode(
                encrypt,
                encrypt ? (ushort)0 : compactLevel,
                !encrypt && compactLevel == 0 ?
                    EncryptionKey256.Zero :
                    (EncryptionKey256?)null); //auto-generate on hash building
        }

        // Static builders.
        // Build a new editable manifest cloning an existing referenced manifest. The original hash
        // reference is lost, but all its nodes are cloned into a fresh editable mantaray that can be
        // modified and hashed again with the given write settings.
        public static async Task<WritableMantarayManifest> BuildFromReferencedManifestAsync(
            ReferencedMantarayManifest referencedManifest,
            IChunkStore chunkStore,
            IPostageStamper postageStamper,
            RedundancyLevel redundancyLevel,
            bool encrypt,
            ushort compactLevel,
            int? chunkHashingConcurrency,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(referencedManifest);
            ArgumentNullException.ThrowIfNull(chunkStore);
            ArgumentNullException.ThrowIfNull(postageStamper);

            // Build an empty manifest with the requested write settings, then clone referenced nodes into its root.
            var manifest = new WritableMantarayManifest(
                chunkStore,
                postageStamper,
                redundancyLevel,
                encrypt,
                compactLevel,
                chunkHashingConcurrency);

            await manifest.rootNode.PopulateFromReferencedNodeAsync(
                (ReferencedMantarayNode)referencedManifest.RootNode,
                cancellationToken).ConfigureAwait(false);

            return manifest;
        }

        // Properties.
        public override IReadOnlyMantarayNode RootNode => rootNode;

        // Methods.
        public void Add(string path, ManifestEntry entry)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(entry);

            rootNode.Add(path, entry);
        }

        public override async Task<SwarmReference> GetReferenceAsync(
            Hasher hasher,
            CancellationToken cancellationToken = default)
        {
            await rootNode.ComputeHashAsync(hasher, hasherPipelineBuilder, cancellationToken).ConfigureAwait(false);
            return rootNode.Reference;
        }
    }
}