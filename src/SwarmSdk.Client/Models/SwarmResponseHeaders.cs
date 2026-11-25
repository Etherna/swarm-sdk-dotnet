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

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http.Headers;

namespace Etherna.SwarmSdk.Models
{
    /// <summary>
    /// The headers a node answers about a content, the same from its download and from its HEAD. They are copied
    /// from the response, so they stay readable once the response is released. The ones a Swarm node describes a
    /// content with are typed, and null when the node doesn't answer them or answers a value that can't be read;
    /// every header, those included, is in <see cref="All"/>
    /// </summary>
    public sealed class SwarmResponseHeaders
    {
        // Constructor.
        /// <param name="headers">The headers of the response, with the ones of its content</param>
        public SwarmResponseHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            ArgumentNullException.ThrowIfNull(headers);

            // Copy the values, under names that ignore the case as HTTP does.
            var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, nameValues) in headers)
            {
                if (!values.TryGetValue(name, out var copiedValues))
                    values[name] = copiedValues = [];
                copiedValues.AddRange(nameValues);
            }
            All = values.ToFrozenDictionary(
                v => v.Key,
                v => (IReadOnlyList<string>)v.Value.AsReadOnly(),
                StringComparer.OrdinalIgnoreCase);

            // Read the typed ones.
            string? FirstValue(string name) =>
                All.TryGetValue(name, out var nameValues) && nameValues.Count > 0 ? nameValues[0] : null;

            if (ContentDispositionHeaderValue.TryParse(
                    FirstValue(SwarmHttpConsts.ContentDispositionHeader), out var contentDisposition))
                ContentDisposition = contentDisposition;

            if (long.TryParse(
                    FirstValue(SwarmHttpConsts.ContentLengthHeader),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var contentLength))
                ContentLength = contentLength;

            //the entity tag is the reference between quotes, and stays so when a proxy marks it as weak
            if (EntityTagHeaderValue.TryParse(FirstValue(SwarmHttpConsts.ETagHeader), out var entityTag) &&
                SwarmReference.TryParse(entityTag.Tag.Trim('"'), null, out var contentReference))
                ContentReference = contentReference;

            if (MediaTypeHeaderValue.TryParse(FirstValue(SwarmHttpConsts.ContentTypeHeader), out var contentType))
                ContentType = contentType;
        }

        // Properties.
        /// <summary>
        /// Every header of the response, the ones of its content among them. A name is found whatever its case
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> All { get; }

        /// <summary>
        /// How the content is meant to be presented, with the file name its manifest gives it
        /// </summary>
        public ContentDispositionHeaderValue? ContentDisposition { get; }

        /// <summary>
        /// The length of the content in bytes
        /// </summary>
        public long? ContentLength { get; }

        /// <summary>
        /// The reference of the content as the node declares it, in the entity tag of its answer. Addressed by a
        /// path of a manifest, it is the reference the path resolves to
        /// </summary>
        public SwarmReference? ContentReference { get; }

        /// <summary>
        /// The media type of the content, with its charset
        /// </summary>
        public MediaTypeHeaderValue? ContentType { get; }

        /// <summary>
        /// True if the content is the one a feed points to
        /// </summary>
        public bool IsFeed => All.ContainsKey(SwarmHttpConsts.SwarmFeedIndexHeader);
    }
}
