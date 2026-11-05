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

using Etherna.SwarmSdk.Models;
using System;
using System.ComponentModel;

namespace Etherna.SwarmSdk.Exceptions
{
    /// <summary>
    /// Data read for a chunk address is not a well formed chunk: its span declares a content no chunk trie can hold,
    /// or its payload doesn't hold the references the span implies
    /// </summary>
    public sealed class SwarmChunkFormatException : Exception
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'hash' argument instead.", error: true)]
        public SwarmChunkFormatException()
        {
            Hash = SwarmHash.Zero;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'hash' argument instead.", error: true)]
        public SwarmChunkFormatException(string? message) : base(message)
        {
            Hash = SwarmHash.Zero;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'hash' argument instead.", error: true)]
        public SwarmChunkFormatException(string? message, Exception? innerException)
            : base(message, innerException)
        {
            Hash = SwarmHash.Zero;
        }
        
        public SwarmChunkFormatException(SwarmHash hash)
        {
            Hash = hash;
        }

        public SwarmChunkFormatException(SwarmHash hash, string message) : base(message)
        {
            Hash = hash;
        }

        public SwarmChunkFormatException(SwarmHash hash, string message, Exception innerException)
            : base(message, innerException)
        {
            Hash = hash;
        }
        
        // Properties.
        /// <summary>
        /// The address the data was read for
        /// </summary>
        public SwarmHash Hash { get; }
    }
}