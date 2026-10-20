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
    /// Data read for a chunk address is not the chunk at that address: its content doesn't verify against the address
    /// </summary>
    public sealed class SwarmChunkVerificationException : Exception
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'hash' argument instead.", error: true)]
        public SwarmChunkVerificationException()
        {
            Hash = SwarmHash.Zero;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'hash' argument instead.", error: true)]
        public SwarmChunkVerificationException(string? message) : base(message)
        {
            Hash = SwarmHash.Zero;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Required by standard compatibility. Don't use; use constructor with 'hash' argument instead.", error: true)]
        public SwarmChunkVerificationException(string? message, Exception? innerException)
            : base(message, innerException)
        {
            Hash = SwarmHash.Zero;
        }
        
        public SwarmChunkVerificationException(SwarmHash hash)
        {
            Hash = hash;
        }

        public SwarmChunkVerificationException(SwarmHash hash, string message) : base(message)
        {
            Hash = hash;
        }

        public SwarmChunkVerificationException(SwarmHash hash, string message, Exception innerException)
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
