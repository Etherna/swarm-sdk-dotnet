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
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Manifest
{
    /// <summary>
    /// A manifest is read by path through its interface alike when writable, before its reference is computed, and
    /// when referenced, read back from a store
    /// </summary>
    public class IReadOnlyMantarayManifestTest
    {
        // Consts.
        private static readonly SwarmReference IndexReference =
            "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9";
        private static readonly SwarmReference PageReference =
            "36b7efd913ca4cf880b8eeac5093fa27b0825906c600685b6abdd6566e6cfe8f";

        // Tests.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetMetadataAsyncAnswersTheMetadataAtThePath(bool referenced)
        {
            // Setup.
            var manifest = await BuildManifestAsync(referenced);

            // Run.
            var metadata = await manifest.GetMetadataAsync("docs/page.html", ManifestPathResolver.IdentityResolver);

            // Assert.
            Assert.Equal("text/html", metadata.Result[ManifestEntry.ContentTypeKey]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetResourceInfoAsyncAnswersTheReferenceAtThePath(bool referenced)
        {
            // Setup.
            var manifest = await BuildManifestAsync(referenced);

            // Run.
            var resource = await manifest.GetResourceInfoAsync(
                "docs/page.html", ManifestPathResolver.IdentityResolver);

            // Assert.
            Assert.Equal(PageReference, resource.Result.Reference);
            Assert.False(resource.IsFromErrorDoc);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetResourceInfoAsyncResolvesTheIndexDocumentAsABrowser(bool referenced)
        {
            // Setup.
            var manifest = await BuildManifestAsync(referenced);

            // Run.
            var resource = await manifest.GetResourceInfoAsync(
                MantarayManifestBase.RootPath, ManifestPathResolver.BrowserResolver);

            // Assert.
            Assert.Equal(IndexReference, resource.Result.Reference);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetResourceInfoAsyncThrowsForAPathTheManifestDoesNotHold(bool referenced)
        {
            // Setup.
            var manifest = await BuildManifestAsync(referenced);

            // Run.
            await Assert.ThrowsAsync<KeyNotFoundException>(() => manifest.GetResourceInfoAsync(
                "docs/missing.html", ManifestPathResolver.IdentityResolver));
        }

        [Theory]
        [InlineData(false, "docs/", true)]
        [InlineData(false, "images/", false)]
        [InlineData(true, "docs/", true)]
        [InlineData(true, "images/", false)]
        public async Task HasPathPrefixAsyncTellsIfAPathStartsWithThePrefix(bool referenced, string prefix, bool expected)
        {
            // Setup.
            var manifest = await BuildManifestAsync(referenced);

            // Run.
            var hasPrefix = await manifest.HasPathPrefixAsync(prefix);

            // Assert.
            Assert.Equal(expected, hasPrefix);
        }

        // Helpers.
        /// <summary>
        /// A site with an index document and a page in a directory: writable and not hashed, or written to a store and
        /// read back
        /// </summary>
        private static async Task<IReadOnlyMantarayManifest> BuildManifestAsync(bool referenced)
        {
            var chunkStore = new MemoryChunkStore();
            var writableManifest = new WritableMantarayManifest(
                chunkStore, new FakePostageStamper(), RedundancyLevel.None, false, 0, null);
            writableManifest.Add(
                MantarayManifestBase.RootPath,
                ManifestEntry.NewDirectory(new Dictionary<string, string>
                {
                    [ManifestEntry.WebsiteIndexDocPathKey] = "index.html"
                }));
            writableManifest.Add(
                "index.html",
                ManifestEntry.NewFile(IndexReference, new Dictionary<string, string>
                {
                    [ManifestEntry.ContentTypeKey] = "text/html"
                }));
            writableManifest.Add(
                "docs/page.html",
                ManifestEntry.NewFile(PageReference, new Dictionary<string, string>
                {
                    [ManifestEntry.ContentTypeKey] = "text/html"
                }));
            if (!referenced)
                return writableManifest;

            var reference = await writableManifest.GetReferenceAsync(new Hasher());
            return await ReferencedMantarayManifest.BuildNewAsync(reference, chunkStore, RedundancyLevel.None);
        }
    }
}
