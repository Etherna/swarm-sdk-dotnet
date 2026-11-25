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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.SwarmSdk.Hashing.Pipeline
{
    /// <summary>
    /// Produce chunked data with span prefix for successive stages.
    /// Also controls the parallelism on chunk elaboration
    /// </summary>
    internal sealed class ChunkFeederPipelineStage : IHasherPipeline
    {
        // Fields.
        private readonly SemaphoreSlim chunkConcurrencySemaphore;
        private readonly ConcurrentQueue<(SemaphoreSlim Semaphore, SwarmChunkBmt SwarmChunkBmt)> chunkResourcesPool;
        private readonly IHasherPipelineStage nextStage;
        private readonly List<Task> nextStageTasks = new();
        
        private long passedBytes;

        // Constructors.
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
        public ChunkFeederPipelineStage(
            IHasherPipelineStage nextStage,
            int? chunkConcurrency = null)
        {
            if (chunkConcurrency <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkConcurrency), "Chunk concurrency must be greater than zero");
            
            chunkConcurrency ??= Environment.ProcessorCount;
            
            this.nextStage = nextStage;
            chunkConcurrencySemaphore = new(chunkConcurrency.Value, chunkConcurrency.Value);
            chunkResourcesPool = new ConcurrentQueue<(SemaphoreSlim Semaphore, SwarmChunkBmt SwarmChunkBmt)>();
            
            //init semaphore pool
            /*
             * Double semaphores compared to current chunk concurrency.
             * This avoids the race condition when: a chunk complete its hashing, it's semaphore is assigned and
             * locked by another one, and only after this the direct child of the first one tries to wait its parent.
             *
             * This is possible because the returning order of semaphores in queue is not guaranteed.
             *
             * Explanation:
             * While the task of chunk A is still waiting to lock on its prev A-1, all the prev chunks have ended tasks.
             * Anyway, prevs didn't end in order, and for some reason semaphore that was of chunk A-1 comes in order
             * before than next "Concurrency -1" (only active task is with A). Because of this, it can be allocated
             * with any next task from A+1. If this happens before A locks on semaphore of A-1, we are in deadlock.
             *
             * Instead, doubling semaphores we guarantee that queue never goes under level of concurrency
             * with contained elements, so a prev chunk's semaphore can't be reused until it's direct next
             * has completed and released concurrency.
             */
            /*
             * We can initialize new ChunkBmts because they are not reusable with different chunk resource pools,
             * and we are not interested into inject mocked ChunkBmts in tests.
             */
            for (int i = 0; i < chunkConcurrency * 2; i++)
                chunkResourcesPool.Enqueue((new SemaphoreSlim(1, 1), new SwarmChunkBmt()));
        }

        // Dispose.
        public void Dispose()
        {
            nextStage.Dispose();
            chunkConcurrencySemaphore.Dispose();
            while (chunkResourcesPool.TryDequeue(out var resources))
                resources.Semaphore.Dispose();
        }
        
        // Properties.
        public bool IsUsable { get; private set; } = true;
        public long MissedOptimisticHashing => nextStage.MissedOptimisticHashing;
        public IPostageStamper PostageStamper => nextStage.PostageStamper;

        // Methods.
        public async Task<SwarmReference> HashDataAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(data);

            using var memoryStream = new MemoryStream(data);
            return await HashDataAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        }
        
        public async Task<SwarmReference> HashDataAsync(Stream dataStream, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dataStream);

            if (!IsUsable)
                throw new InvalidOperationException("Pipeline has already been used");
            
            // Make it no more usable.
            IsUsable = false;
            
            // Slicing the stream permits to avoid to load all the stream in memory at the same time.
            var chunkBuffer = new byte[SwarmCac.DataSize];
            bool isEndOfStream = false;
            SemaphoreSlim? prevChunkSemaphore = null;
            try
            {
                while (!isEndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    (var chunkReadSize, isEndOfStream) = await ReadChunkDataFromStreamAsync(
                        dataStream,
                        chunkBuffer,
                        cancellationToken).ConfigureAwait(false);
                
                    if (chunkReadSize > 0 || //write only chunks with data
                        passedBytes == 0)    //or accept also edge case trying to hash an empty stream
                    {
                        var chunkNumberId = passedBytes / SwarmCac.DataSize;
                    
                        // Copy read data from buffer to a new chunk data byte[]. Include also span
                        var chunkData = new byte[SwarmCac.SpanSize + chunkReadSize];
                        chunkBuffer.AsSpan(0, chunkReadSize).CopyTo(chunkData.AsSpan(SwarmCac.SpanSize));
                    
                        // Write chunk span.
                        SwarmCac.WriteSpan((ulong)chunkReadSize, chunkData.AsSpan(0, SwarmCac.SpanSize));
                
                        // Invoke next stage with parallelism on chunks.
                        //control concurrency
                        await chunkConcurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    
                        //initialize chunk semaphore, receiving from resources pool
#pragma warning disable CA2000
                        if (!chunkResourcesPool.TryDequeue(out var chunkResources))
                            throw new InvalidOperationException("Chunk resources pool exhausted");
#pragma warning restore CA2000
                        //not cancellable: a resource taken from the pool goes back to it only through its chunk task
                        await chunkResources.Semaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                    
                        //build args
                        var feedArgs = new HasherPipelineFeedArgs(
                            swarmChunkBmt: chunkResources.SwarmChunkBmt,
                            span: chunkData.AsMemory()[..SwarmCac.SpanSize],
                            spanData: chunkData,
                            numberId: chunkNumberId,
                            prevChunkSemaphore: prevChunkSemaphore,
                            cancellationToken: cancellationToken);
                    
                        //run task
                        nextStageTasks.Add(
                            Task.Run(async () =>
                            {
                                try
                                {
                                    await nextStage.FeedAsync(feedArgs).ConfigureAwait(false);
                                }
                                finally
                                {
                                    //release and restore chunk semaphore in pool
                                    chunkResources.Semaphore.Release();
                                    chunkResourcesPool.Enqueue(chunkResources);
                                
                                    //release task for next chunk
                                    chunkConcurrencySemaphore.Release();
                                }
                            },
                            //not cancellable: a task that doesn't start never releases the resources taken for it
                            CancellationToken.None));
                    
                        //set current chunk semaphore as prev for next chunk
                        prevChunkSemaphore = chunkResources.Semaphore;
                    
                        passedBytes += chunkReadSize;
                    }
                }
            }
            catch
            {
                // Whatever stopped the feed, a cancellation or a failed read, the chunks in flight hold resources
                // of the pool: let them end before leaving.
                await Task.WhenAll(nextStageTasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                throw;
            }

            // Wait the end of all chunk computation.
            await Task.WhenAll(nextStageTasks).ConfigureAwait(false);

            // Extract an unused chunk's hasher and sum.
            chunkResourcesPool.TryDequeue(out var resourceTuple);
            return await nextStage.SumAsync(resourceTuple.SwarmChunkBmt, cancellationToken).ConfigureAwait(false);
        }

        // Helpers.
        private static async Task<(int chunkDataSize, bool isEndOfStream)> ReadChunkDataFromStreamAsync(
            Stream dataStream,
            Memory<byte> chunkBuffer,
            CancellationToken cancellationToken)
        {
            var isEndOfStream = false;
            var totalDataSize = 0;

            do
            {
                var dataSize = await dataStream.ReadAsync(chunkBuffer[totalDataSize..], cancellationToken).ConfigureAwait(false);
                if (dataSize == 0)
                    isEndOfStream = true;
                totalDataSize += dataSize;
            } while (!isEndOfStream &&
                     totalDataSize < chunkBuffer.Length);
            
            return (totalDataSize, isEndOfStream);
        }
    }
}