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

using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Hashing.Pipeline
{
    internal sealed class ChunkStoreWriterPipelineStage(
        IChunkStore chunkStore,
        IPostageStamper postageStamper,
        IHasherPipelineStage? nextStage,
        bool readOnly)
        : IHasherPipelineStage
    {
        // Dispose.
        public void Dispose()
        {
            nextStage?.Dispose();
        }
        
        // Properties.
        public long MissedOptimisticHashing => nextStage?.MissedOptimisticHashing ?? 0;
        public IPostageStamper PostageStamper => postageStamper;

        // Methods.
        public async Task FeedAsync(HasherPipelineFeedArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            if (args.Reference is null) throw new InvalidOperationException();

            if (!readOnly)
            {
                // Stamp chunk and store stamp.
                postageStamper.Stamp(args.Reference.Value.Hash);
            
                // Store chunk.
                var chunk = new SwarmCac(args.Reference.Value.Hash, args.SpanData);
                await chunkStore.AddAsync(chunk, args.CancellationToken).ConfigureAwait(false);
            }

            if (nextStage is not null)
                await nextStage.FeedAsync(args).ConfigureAwait(false);
        }
        
        public Task<SwarmReference> SumAsync(SwarmChunkBmt swarmChunkBmt, CancellationToken cancellationToken) =>
            nextStage?.SumAsync(swarmChunkBmt, cancellationToken) ?? throw new InvalidOperationException();
    }
}