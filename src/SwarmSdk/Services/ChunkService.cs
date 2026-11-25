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
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Manifest;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Services
{
    public class ChunkService : IChunkService
    {
        // Methods.
        public async Task<Stream> GetFileStreamFromAddressAsync(
            SwarmAddress address,
            ManifestPathResolver manifestPathResolver,
            IReadOnlyChunkStore chunkStore,
            RedundancyLevel redundancyLevel = RedundancyLevel.Paranoid,
            RedundancyStrategy redundancyStrategy = RedundancyStrategy.Data,
            bool redundancyStrategyFallback = true,
            CancellationToken cancellationToken = default)
        {
            var chunkReference = (await SwarmAddressResolver.ResolveResourceInfoAsync(
                address,
                chunkStore,
                manifestPathResolver,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback,
                cancellationToken).ConfigureAwait(false)).Result.Reference;

            return await ChunkDataStream.BuildNewAsync(
                chunkReference,
                chunkStore,
                redundancyLevel,
                redundancyStrategy,
                redundancyStrategyFallback,
                cancellationToken).ConfigureAwait(false);
        }

        public Task<UploadEvaluationResult> UploadDirectoryAsync(
            string directoryPath,
            Hasher hasher,
            string? indexFilename = null,
            string? errorFilename = null,
            ushort compactLevel = 0,
            bool encrypt = false,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            IPostageStamper? postageStamper = null,
            int? chunkCuncorrency = null, 
            IChunkStore? chunkStore = null,
            CancellationToken cancellationToken = default)
        {
            // Get all files from directory.
            var files = Directory.GetFiles(directoryPath, "", SearchOption.AllDirectories);

            // Evaluate upload.
            return UploadDirectoryAsync(
                files.Select(f => Path.GetRelativePath(directoryPath, f)).ToArray(),
                f =>  File.OpenRead(Path.Combine(directoryPath, f)),
                hasher,
                indexFilename,
                errorFilename,
                compactLevel,
                encrypt,
                redundancyLevel,
                postageStamper,
                chunkCuncorrency,
                chunkStore,
                cancellationToken);
        }

        public async Task<UploadEvaluationResult> UploadDirectoryAsync(
            string[] fileNames,
            Func<string, Stream> getFileStream,
            Hasher hasher,
            string? indexFilename = null,
            string? errorFilename = null,
            ushort compactLevel = 0,
            bool encrypt = false,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            IPostageStamper? postageStamper = null,
            int? chunkCuncorrency = null,
            IChunkStore? chunkStore = null,
            CancellationToken cancellationToken = default)
        {
            // Checks.
            ArgumentNullException.ThrowIfNull(fileNames);
            ArgumentNullException.ThrowIfNull(getFileStream);
            
            if (fileNames.Length == 0)
                throw new ArgumentException("No files in directory", nameof(fileNames));
            if (fileNames.Any(f => f.StartsWith(SwarmAddress.Separator)))
                throw new ArgumentException(
                    "File names can't start with slash character",
                    nameof(fileNames));
            if (indexFilename?.Contains(SwarmAddress.Separator, StringComparison.InvariantCulture) == true)
                throw new ArgumentException(
                    "Index document suffix must not include slash character",
                    nameof(indexFilename));
            if (errorFilename?.Contains(SwarmAddress.Separator, StringComparison.InvariantCulture) == true)
                throw new ArgumentException(
                    "Error document suffix must not include slash character",
                    nameof(errorFilename));
            
            // Init.
            chunkStore ??= new FakeChunkStore();
            postageStamper ??= new PostageStamper(
                new FakeSigner(),
                new PostageStampIssuer(PostageBatch.MaxDepthInstance),
                new MemoryStampStore());
            long totalMissedOptimisticHashing = 0;
            
            // Try set index document.
            if (indexFilename is null && fileNames.Contains(SwarmHttpConsts.DefaultIndexFilename))
                indexFilename = SwarmHttpConsts.DefaultIndexFilename;
            
            // Create manifest.
            var dirManifest = new WritableMantarayManifest(
                chunkStore,
                postageStamper,
                redundancyLevel,
                encrypt,
                compactLevel,
                chunkCuncorrency);
            
            // Iterate through the files.
            foreach (var filePath in fileNames)
            {
                using var fileHasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                    chunkStore,
                    postageStamper,
                    redundancyLevel,
                    encrypt,
                    compactLevel,
                    chunkCuncorrency);
                
                var fileContentType = FileContentTypeProvider.GetContentType(filePath);
                var fileName = Path.GetFileName(filePath);
                var fileStream = getFileStream(filePath);
                await using (fileStream.ConfigureAwait(false))
                {
                    var fileReference = await fileHasherPipeline.HashDataAsync(
                        fileStream,
                        cancellationToken).ConfigureAwait(false);
                    totalMissedOptimisticHashing += fileHasherPipeline.MissedOptimisticHashing;
                
                    // Add file entry to dir manifest.
                    dirManifest.Add(
                        filePath,
                        ManifestEntry.NewFile(
                            fileReference,
                            new Dictionary<string, string>
                            {
                                [ManifestEntry.ContentTypeKey] = fileContentType,
                                [ManifestEntry.FilenameKey] = fileName
                            }));
                }
            }
            
            // Store website information.
            if (!string.IsNullOrEmpty(indexFilename) ||
                !string.IsNullOrEmpty(errorFilename))
            {
                var metadata = new Dictionary<string, string>();
                
                if (!string.IsNullOrEmpty(indexFilename))
                    metadata[ManifestEntry.WebsiteIndexDocPathKey] = indexFilename;
                if (!string.IsNullOrEmpty(errorFilename))
                    metadata[ManifestEntry.WebsiteErrorDocPathKey] = errorFilename;

                var rootManifestEntry = ManifestEntry.NewDirectory(metadata);
                dirManifest.Add(MantarayManifestBase.RootPath, rootManifestEntry);
            }

            // Get manifest hash.
            var chunkHashingResult = await dirManifest.GetReferenceAsync(hasher, cancellationToken).ConfigureAwait(false);
            
            // Return result.
            return new UploadEvaluationResult(
                chunkHashingResult,
                totalMissedOptimisticHashing,
                postageStamper.StampIssuer);
        }

        public async Task<UploadEvaluationResult> UploadSingleFileAsync(
            byte[] data,
            string fileContentType,
            string? fileName,
            Hasher hasher,
            ushort compactLevel = 0,
            bool encrypt = false,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            IPostageStamper? postageStamper = null,
            int? chunkCuncorrency = null, 
            IChunkStore? chunkStore = null,
            CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream(data);
            return await UploadSingleFileAsync(
                stream,
                fileContentType,
                fileName,
                hasher,
                compactLevel,
                encrypt,
                redundancyLevel,
                postageStamper,
                chunkCuncorrency,
                chunkStore,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<UploadEvaluationResult> UploadSingleFileAsync(
            Stream stream,
            string fileContentType,
            string? fileName,
            Hasher hasher,
            ushort compactLevel = 0,
            bool encrypt = false,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            IPostageStamper? postageStamper = null,
            int? chunkCuncorrency = null, 
            IChunkStore? chunkStore = null,
            CancellationToken cancellationToken = default)
        {
            // Checks.
            if (fileName?.Contains(SwarmAddress.Separator, StringComparison.InvariantCulture) == true)
                throw new ArgumentException(
                    "File name must not include slash character",
                    nameof(fileName));

            // Init.
            chunkStore ??= new FakeChunkStore();
            postageStamper ??= new PostageStamper(
                new FakeSigner(),
                new PostageStampIssuer(PostageBatch.MaxDepthInstance),
                new MemoryStampStore());
            
            // Get file hash.
            using var fileHasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStore,
                postageStamper,
                redundancyLevel,
                encrypt,
                compactLevel,
                chunkCuncorrency);
            var fileReference = await fileHasherPipeline.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            
            // If file name is null or empty, use the file hash as name.
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = fileReference.ToString();
            
            // Create manifest.
            var manifest = new WritableMantarayManifest(
                chunkStore,
                postageStamper,
                redundancyLevel,
                encrypt,
                compactLevel,
                chunkCuncorrency);
            
            manifest.Add(
                MantarayManifestBase.RootPath,
                ManifestEntry.NewDirectory(
                    new Dictionary<string, string>
                    {
                        [ManifestEntry.WebsiteIndexDocPathKey] = fileName,
                    }));
            
            manifest.Add(
                fileName,
                ManifestEntry.NewFile(
                    fileReference,
                    new Dictionary<string, string>
                    {
                        [ManifestEntry.ContentTypeKey] = fileContentType,
                        [ManifestEntry.FilenameKey] = fileName
                    }));

            var chunkHashingResult = await manifest.GetReferenceAsync(hasher, cancellationToken).ConfigureAwait(false);
            
            // Return result.
            return new UploadEvaluationResult(
                chunkHashingResult,
                fileHasherPipeline.MissedOptimisticHashing,
                postageStamper.StampIssuer);
        }

        public Task<SwarmReference> WriteDataChunksAsync(
            IChunkStore chunkStore,
            byte[] data,
            IPostageStampIssuer? postageStampIssuer = null,
            ushort compactLevel = 0,
            bool encrypt = false,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            int? chunkCuncorrency = null,
            CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2025
            using var stream = new MemoryStream(data);
            return WriteDataChunksAsync(
                chunkStore,
                stream,
                postageStampIssuer,
                compactLevel,
                encrypt,
                redundancyLevel,
                chunkCuncorrency,
                cancellationToken);
#pragma warning restore CA2025
        }

        public async Task<SwarmReference> WriteDataChunksAsync(
            IChunkStore chunkStore,
            Stream stream,
            IPostageStampIssuer? postageStampIssuer = null,
            ushort compactLevel = 0,
            bool encrypt = false,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            int? chunkCuncorrency = null,
            CancellationToken cancellationToken = default)
        {
            postageStampIssuer ??= new PostageStampIssuer(PostageBatch.MaxDepthInstance);
            var postageStamper = new PostageStamper(
                new FakeSigner(),
                postageStampIssuer,
                new MemoryStampStore());
            
            // Create chunks and get file reference.
            using var fileHasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStore,
                postageStamper,
                redundancyLevel,
                encrypt,
                compactLevel,
                chunkCuncorrency);
            return await fileHasherPipeline.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        }
    }
}