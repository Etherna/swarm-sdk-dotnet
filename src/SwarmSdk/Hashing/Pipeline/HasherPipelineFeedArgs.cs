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
using System.Threading;

namespace Etherna.SwarmSdk.Hashing.Pipeline
{
    public sealed class HasherPipelineFeedArgs
    {
        // Constructor.
        public HasherPipelineFeedArgs(
            SwarmChunkBmt swarmChunkBmt,
            ReadOnlyMemory<byte> span,
            ReadOnlyMemory<byte> spanData,
            long numberId = 0,
            SemaphoreSlim? prevChunkSemaphore = null,
            CancellationToken cancellationToken = default)
        {
            if (span.Length != SwarmCac.SpanSize)
                throw new ArgumentOutOfRangeException(nameof(span), $"Span must have length of {SwarmCac.SpanSize}");
            if (spanData.Length < SwarmCac.SpanSize)
                throw new InvalidOperationException("Data must contain also span, if present");
            
            CancellationToken = cancellationToken;
            Span = span;
            SpanData = spanData;
            SwarmChunkBmt = swarmChunkBmt;
            NumberId = numberId;
            PrevChunkSemaphore = prevChunkSemaphore;
        }
        
        // Properties.
        /// <summary>
        /// Cancellation token of the pipeline run the chunk belongs to
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Ordered id, from 0 to n with the last chunk
        /// </summary>
        public long NumberId { get; }

        /// <summary>
        /// Previous chunk semaphore. Occupied resource until chunk is processing.
        /// </summary>
        public SemaphoreSlim? PrevChunkSemaphore { get; }
        
        public SwarmReference? Reference { get; internal set; }

        /// <summary>
        /// Always not encrypted span uint64
        /// </summary>
        public ReadOnlyMemory<byte> Span { get; }

        /// <summary>
        /// SpanData may be encrypted if the pipeline is encrypted
        /// </summary>
        public ReadOnlyMemory<byte> SpanData { get; internal set; }

        public SwarmChunkBmt SwarmChunkBmt { get; }
    }
}