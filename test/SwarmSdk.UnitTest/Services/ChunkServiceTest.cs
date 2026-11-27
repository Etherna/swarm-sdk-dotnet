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
using Etherna.SwarmSdk.Manifest;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Services
{
    public class ChunkServiceTest
    {
        // Consts.
        private const long TwoLevelsLength = SwarmCac.DataSize * 128L * 2 + 5;

        // Data.
        public static TheoryData<long, RedundancyLevel, bool, ushort> PipelineUploads
        {
            get
            {
                var data = new TheoryData<long, RedundancyLevel, bool, ushort>();
                long[] lengths =
                [
                    0, 1, SwarmCac.DataSize, SwarmCac.DataSize + 1,
                    SwarmCac.DataSize * 64L, SwarmCac.DataSize * 64L + 1,
                    SwarmCac.DataSize * 128L, SwarmCac.DataSize * 128L + 1, TwoLevelsLength
                ];
                foreach (var redundancyLevel in Enum.GetValues<RedundancyLevel>())
                foreach (var encrypt in new[] { false, true })
                foreach (var length in lengths)
                    data.Add(length, redundancyLevel, encrypt, 0);

                //three levels: the branching of an intermediate chunk is 20 references at Paranoid encrypted, 39
                //at Paranoid plain, so two levels hold 1,638,400 and 6,230,016 bytes
                data.Add(1_638_400L * 2 + 7, RedundancyLevel.Paranoid, true, 0);
                data.Add(1_638_400L + SwarmCac.DataSize * 20L * 3, RedundancyLevel.Paranoid, true, 0);
                data.Add(6_230_016L + 1, RedundancyLevel.Paranoid, false, 0);

                //a compacted upload references its chunks with their key, as an encrypted one
                data.Add(TwoLevelsLength, RedundancyLevel.None, false, 1);
                data.Add(TwoLevelsLength, RedundancyLevel.Paranoid, false, 1);
                return data;
            }
        }

        // Tests.
        [Theory]
        [MemberData(nameof(PipelineUploads))]
        public async Task CountContentChunksCountsTheChunksThePipelineWrites(
            long dataLength,
            RedundancyLevel redundancyLevel,
            bool encrypt,
            ushort compactLevel)
        {
            // Setup.
            var data = new byte[dataLength];
            new Random(0).NextBytes(data);
            var writes = 0L;
            var chunkStoreMock = new Mock<IChunkStore>();
            chunkStoreMock
                .Setup(s => s.AddAsync(It.IsAny<SwarmChunk>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    Interlocked.Increment(ref writes);
                    return Task.FromResult(true);
                });
            using var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStoreMock.Object, new FakePostageStamper(), redundancyLevel, encrypt, compactLevel, null);
            await pipeline.HashDataAsync(data);

            // Run.
            var count = new ChunkService().CountContentChunks(dataLength, redundancyLevel, encrypt, compactLevel);

            // Assert.
            Assert.Equal(writes, count);
        }

        [Fact]
        public void CountContentChunksRejectsANegativeLength() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkService().CountContentChunks(-1));

        /// <summary>
        /// A manifest of one file is the one of a single file upload. With 30 files the root node fills one chunk to
        /// its last byte (4096); with 40 it holds 5376 bytes, and spans a chunk tree
        /// </summary>
        [Theory]
        [InlineData(1, RedundancyLevel.None, false, 0)]
        [InlineData(30, RedundancyLevel.None, false, 0)]
        [InlineData(40, RedundancyLevel.None, false, 0)]
        [InlineData(40, RedundancyLevel.None, true, 0)]
        [InlineData(40, RedundancyLevel.Medium, false, 0)]
        [InlineData(40, RedundancyLevel.Paranoid, true, 0)]
        [InlineData(40, RedundancyLevel.Strong, false, 1)]
        public async Task CountManifestChunksCountsTheChunksTheManifestWritesWithPlaceholderReferences(
            int files,
            RedundancyLevel redundancyLevel,
            bool encrypt,
            ushort compactLevel)
        {
            // Setup.
            //files at the root, each at a path of its own first character, with the first as index document
            var random = new Random(0);
            var paths = Enumerable.Range(0, files)
                .Select(i => $"{(char)(i < 26 ? 'a' + i : 'A' + i - 26)}-file-{i}.html")
                .ToArray();
            ManifestEntry BuildFileEntry(string path, SwarmReference reference) =>
                ManifestEntry.NewFile(reference, new Dictionary<string, string>
                {
                    [ManifestEntry.ContentTypeKey] = "text/html",
                    [ManifestEntry.FilenameKey] = path
                });
            KeyValuePair<string, ManifestEntry>[] BuildEntries(Func<SwarmReference> buildReference) =>
            [
                new(MantarayManifestBase.RootPath, ManifestEntry.NewDirectory(new Dictionary<string, string>
                {
                    [ManifestEntry.WebsiteIndexDocPathKey] = paths[0]
                })),
                .. paths.Select(path => new KeyValuePair<string, ManifestEntry>(path, BuildFileEntry(path, buildReference())))
            ];

            var writes = 0L;
            var chunkStoreMock = new Mock<IChunkStore>();
            chunkStoreMock
                .Setup(s => s.AddAsync(It.IsAny<SwarmChunk>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    Interlocked.Increment(ref writes);
                    return Task.FromResult(true);
                });
            var manifest = new WritableMantarayManifest(
                chunkStoreMock.Object, new FakePostageStamper(), redundancyLevel, encrypt, compactLevel, null);
            foreach (var (path, entry) in BuildEntries(() =>
                     {
                         var reference = new byte[encrypt ? SwarmReference.EncryptedSize : SwarmReference.PlainSize];
                         random.NextBytes(reference);
                         return reference;
                     }))
                manifest.Add(path, entry);
            await manifest.GetReferenceAsync(new Hasher());

            // Run.
            var count = await new ChunkService().CountManifestChunksAsync(
                BuildEntries(() => encrypt ? SwarmReference.EncryptedZero : SwarmReference.PlainZero),
                redundancyLevel,
                encrypt);

            // Assert.
            Assert.Equal(writes, count);
        }

        /// <summary>
        /// A store completing its writes asynchronously makes the pipeline read the content after the call has handed
        /// back its task: the array overload must keep its stream open until the pipeline ends
        /// </summary>
        [Fact]
        public async Task WriteDataChunksAsyncReadsAnArrayPastTheFirstAsynchronousWrite()
        {
            // Setup.
            var data = new byte[SwarmCac.DataSize * 3];
            new Random(0).NextBytes(data);
            var writesGate = new TaskCompletionSource();
            var chunkStoreMock = new Mock<IChunkStore>();
            chunkStoreMock
                .Setup(s => s.AddAsync(It.IsAny<SwarmChunk>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await writesGate.Task;
                    return true;
                });
            var expected = await new ChunkService().WriteDataChunksAsync(new FakeChunkStore(), new MemoryStream(data));

            // Run.
            //with one chunk at a time, the second chunk waits for the first write, held until the call has returned
            var writeTask = new ChunkService().WriteDataChunksAsync(chunkStoreMock.Object, data, chunkCuncorrency: 1);
            writesGate.SetResult();
            var reference = await writeTask;

            // Assert.
            Assert.Equal(expected, reference);
        }
    }
}
