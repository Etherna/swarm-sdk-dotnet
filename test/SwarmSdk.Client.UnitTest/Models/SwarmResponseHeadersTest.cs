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
using Xunit;

namespace Etherna.SwarmSdk.Models
{
    public class SwarmResponseHeadersTest
    {
        // Consts.
        private const string EncryptedReference =
            "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9" +
            "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";
        private const string PlainReference = "1231dd7f156bc47de7dadd3b0bac0ac07dcb47f45d3528192e9e25574c8744b9";

        // Tests.
        [Fact]
        public void AllFindsAHeaderWhateverTheCaseOfItsName()
        {
            // Setup.
            var headers = new SwarmResponseHeaders(new Dictionary<string, IEnumerable<string>>
            {
                ["swarm-feed-index"] = ["0000000000000003"]
            });

            // Assert.
            Assert.Equal("0000000000000003", Assert.Single(headers.All["Swarm-Feed-Index"]));
            Assert.Equal("0000000000000003", Assert.Single(headers.All["SWARM-FEED-INDEX"]));
            Assert.Single(headers.All);
        }

        [Fact]
        public void AllJoinsTheValuesOfNamesDifferingByTheirCase()
        {
            // Setup.
            var headers = new SwarmResponseHeaders(new Dictionary<string, IEnumerable<string>>
            {
                ["Access-Control-Expose-Headers"] = ["Accept-Ranges"],
                ["access-control-expose-headers"] = ["Content-Disposition", "Swarm-Feed-Index"]
            });

            // Assert.
            Assert.Equal(
                ["Accept-Ranges", "Content-Disposition", "Swarm-Feed-Index"],
                headers.All["Access-Control-Expose-Headers"]);
        }

        [Fact]
        public void AllKeepsWhatItReadWhenTheSourceChanges()
        {
            // Setup.
            var values = new List<string> { "1234" };
            var source = new Dictionary<string, IEnumerable<string>> { ["Content-Length"] = values };
            var headers = new SwarmResponseHeaders(source);

            // Run.
            values.Add("5678");
            source["ETag"] = [$"\"{PlainReference}\""];

            // Assert.
            Assert.Equal("1234", Assert.Single(Assert.Single(headers.All).Value));
        }

        [Fact]
        public void ContentDispositionCarriesTheFileName()
        {
            // Setup.
            var headers = new SwarmResponseHeaders(new Dictionary<string, IEnumerable<string>>
            {
                ["Content-Disposition"] = ["inline; filename=\"index.html\""]
            });

            // Assert.
            Assert.Equal("inline", headers.ContentDisposition?.DispositionType);
            Assert.Equal("index.html", headers.ContentDisposition?.FileName?.Trim('"'));
        }

        [Theory]
        [InlineData("1234", 1234L)]
        [InlineData("0", 0L)]
        [InlineData("-1", null)]
        [InlineData("many", null)]
        [InlineData("", null)]
        public void ContentLengthIsTheDeclaredOneOrNull(string value, long? expected)
        {
            // Setup.
            var headers = new SwarmResponseHeaders(new Dictionary<string, IEnumerable<string>>
            {
                ["Content-Length"] = [value]
            });

            // Assert.
            Assert.Equal(expected, headers.ContentLength);
        }

        [Theory]
        [InlineData($"\"{PlainReference}\"", PlainReference)] //as a bee node declares it
        [InlineData($"W/\"{PlainReference}\"", PlainReference)] //as a proxy compressing the answer rewrites it
        [InlineData($"\"{EncryptedReference}\"", EncryptedReference)]
        [InlineData("\"5d8c72a5edda8\"", null)] //the entity tag of another server
        [InlineData(PlainReference, null)] //not an entity tag
        [InlineData("", null)]
        public void ContentReferenceIsTheOneOfTheEntityTagOrNull(string entityTag, string? expected)
        {
            // Setup.
            var headers = new SwarmResponseHeaders(new Dictionary<string, IEnumerable<string>>
            {
                ["ETag"] = [entityTag]
            });

            // Assert.
            Assert.Equal(expected, headers.ContentReference?.ToString());
        }

        [Theory]
        [InlineData("text/html; charset=utf-8", "text/html", "utf-8")]
        [InlineData("application/octet-stream", "application/octet-stream", null)]
        [InlineData("no media type", null, null)]
        public void ContentTypeIsTheDeclaredOneOrNull(string value, string? expectedMediaType, string? expectedCharSet)
        {
            // Setup.
            var headers = new SwarmResponseHeaders(new Dictionary<string, IEnumerable<string>>
            {
                ["Content-Type"] = [value]
            });

            // Assert.
            Assert.Equal(expectedMediaType, headers.ContentType?.MediaType);
            Assert.Equal(expectedCharSet, headers.ContentType?.CharSet);
        }

        [Theory]
        [InlineData("Swarm-Feed-Index", true)]
        [InlineData("swarm-feed-index", true)]
        [InlineData("Swarm-Feed-Index-Next", false)]
        public void IsFeedTellsIfTheNodeAnswersAFeedIndex(string headerName, bool expected)
        {
            // Setup.
            var headers = new SwarmResponseHeaders(new Dictionary<string, IEnumerable<string>>
            {
                [headerName] = ["0000000000000003"]
            });

            // Assert.
            Assert.Equal(expected, headers.IsFeed);
        }

        [Fact]
        public void TypedHeadersAreNullWhenTheNodeAnswersNone()
        {
            // Setup.
            var headers = new SwarmResponseHeaders(new Dictionary<string, IEnumerable<string>>());

            // Assert.
            Assert.Empty(headers.All);
            Assert.Null(headers.ContentDisposition);
            Assert.Null(headers.ContentLength);
            Assert.Null(headers.ContentReference);
            Assert.Null(headers.ContentType);
            Assert.False(headers.IsFeed);
        }
    }
}
