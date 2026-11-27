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
using Etherna.SwarmSdk.Services;
using Etherna.SwarmSdk.Stores;
using Etherna.SwarmSdk.Wasm.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Wasm.Contents
{
    /// <summary>
    /// Chunk a content into a chunk store, stamping its chunks: the work shared by the exports that only hash
    /// a content and by the ones that upload it. A null chunk concurrency takes the default of the SDK.
    /// </summary>
    internal static class ContentWriter
    {
        // Methods.
        /// <summary>
        /// Chunk raw bytes, as a bee node does on its bytes endpoint.
        /// </summary>
        public static async Task<ContentResult> WriteBytesAsync(
            byte[] data,
            ContentOptions options,
            IChunkStore chunkStore,
            IPostageStamper postageStamper,
            int? chunkConcurrency)
        {
            using var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStore,
                postageStamper,
                options.RedundancyLevel,
                options.Encrypt,
                options.CompactLevel,
                chunkConcurrency);
            var reference = await pipeline.HashDataAsync(data).ConfigureAwait(false);
            return ContentResult.Build(
                reference,
                pipeline.MissedOptimisticHashing,
                postageStamper.StampIssuer.Buckets);
        }

        /// <summary>
        /// Chunk a directory, as a bee node does on its bzz endpoint with a tar collection. The files are given
        /// as their paths, the concatenation of their contents in the same order, and the length of each
        /// content.
        /// </summary>
        public static async Task<ContentResult> WriteDirectoryAsync(
            string[] filePaths,
            byte[] contents,
            int[] contentLengths,
            string? indexFilename,
            string? errorFilename,
            ContentOptions options,
            IChunkStore chunkStore,
            IPostageStamper postageStamper,
            int? chunkConcurrency)
        {
            ArgumentNullException.ThrowIfNull(filePaths);
            ArgumentNullException.ThrowIfNull(contents);
            ArgumentNullException.ThrowIfNull(contentLengths);
            if (filePaths.Length != contentLengths.Length)
                throw new ArgumentException("Every file needs its content length", nameof(contentLengths));

            //find where the content of each file starts
            Dictionary<string, (int Offset, int Length)> fileSegments = [];
            var offset = 0;
            for (var i = 0; i < filePaths.Length; i++)
            {
                if (contentLengths[i] < 0 || contentLengths[i] > contents.Length - offset)
                    throw new ArgumentException("Content lengths exceed the contents", nameof(contentLengths));
                if (!fileSegments.TryAdd(filePaths[i], (offset, contentLengths[i])))
                    throw new ArgumentException($"Duplicate file path {filePaths[i]}", nameof(filePaths));
                offset += contentLengths[i];
            }
            if (offset != contents.Length)
                throw new ArgumentException("Content lengths don't cover the contents", nameof(contentLengths));

            var result = await new ChunkService().UploadDirectoryAsync(
                filePaths,
                path =>
                {
                    var (fileOffset, fileLength) = fileSegments[path];
                    return new MemoryStream(contents, fileOffset, fileLength, false);
                },
                new Hasher(),
                indexFilename,
                errorFilename,
                options.CompactLevel,
                options.Encrypt,
                options.RedundancyLevel,
                postageStamper,
                chunkConcurrency,
                chunkStore).ConfigureAwait(false);
            return ContentResult.Build(
                result.Reference,
                result.MissedOptimisticHashing,
                result.PostageStampIssuer.Buckets);
        }

        /// <summary>
        /// Chunk a single file, as a bee node does on its bzz endpoint.
        /// </summary>
        public static async Task<ContentResult> WriteFileAsync(
            byte[] data,
            string contentType,
            string? fileName,
            ContentOptions options,
            IChunkStore chunkStore,
            IPostageStamper postageStamper,
            int? chunkConcurrency)
        {
            var result = await new ChunkService().UploadSingleFileAsync(
                data,
                contentType,
                fileName,
                new Hasher(),
                options.CompactLevel,
                options.Encrypt,
                options.RedundancyLevel,
                postageStamper,
                chunkConcurrency,
                chunkStore).ConfigureAwait(false);
            return ContentResult.Build(
                result.Reference,
                result.MissedOptimisticHashing,
                result.PostageStampIssuer.Buckets);
        }
    }
}
