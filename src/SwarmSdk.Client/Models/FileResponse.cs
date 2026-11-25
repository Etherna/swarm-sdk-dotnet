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
using System.IO;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Models
{
    /// <summary>
    /// A content a node answers: its headers, the same a HEAD of it answers, and the stream of its data
    /// </summary>
    public sealed class FileResponse(
        SwarmResponseHeaders headers,
        Stream stream)
        : IDisposable, IAsyncDisposable
    {
        // Dispose.
        public void Dispose() => Stream.Dispose();
        public ValueTask DisposeAsync() => Stream.DisposeAsync();

        // Properties.
        public SwarmResponseHeaders Headers { get; } = headers;
        public Stream Stream { get; } = stream;
    }
}
