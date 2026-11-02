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

using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Time.Testing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Services
{
    public class PostageBatchUsablePollerTest
    {
        // Consts.
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

        // Tests.
        [Fact]
        public async Task ReturnsImmediatelyWhenBatchAlreadyUsable()
        {
            // Setup.
            var callCount = 0;
            Task<PostageBatch> GetBatch(PostageBatchId id, CancellationToken _)
            {
                callCount++;
                return Task.FromResult(Batch(id, isUsable: true));
            }

            // Run.
            var result = await PostageBatchUsablePoller.WaitUntilUsableAsync(
                PostageBatchId.Zero, GetBatch, timeProvider: new FakeTimeProvider());

            // Assert.
            Assert.True(result.IsUsable);
            Assert.Equal(1, callCount); // no polling needed when already usable
        }

        [Fact]
        public async Task PollsUntilBatchBecomesUsable()
        {
            // Setup.
            // The batch reports not-usable for the first two checks, then usable: a virtual clock
            // releases each poll interval so the staged transition is observed deterministically.
            var fakeTimeProvider = new FakeTimeProvider();
            var outcomes = new Queue<bool>([false, false, true]);
            var callCount = 0;
            Task<PostageBatch> GetBatch(PostageBatchId id, CancellationToken _)
            {
                callCount++;
                return Task.FromResult(Batch(id, isUsable: outcomes.Dequeue()));
            }

            // Run.
            var waitTask = PostageBatchUsablePoller.WaitUntilUsableAsync(
                PostageBatchId.Zero, GetBatch, pollInterval: PollInterval, timeProvider: fakeTimeProvider);
            await DriveVirtualClockUntilCompletedAsync(fakeTimeProvider, waitTask, PollInterval);
            var result = await waitTask;

            // Assert.
            Assert.True(result.IsUsable);
            Assert.Equal(3, callCount);
        }

        [Fact]
        public async Task ThrowsTimeoutWhenBatchNeverBecomesUsable()
        {
            // Setup.
            var fakeTimeProvider = new FakeTimeProvider();
            var timeout = TimeSpan.FromSeconds(20);
            Task<PostageBatch> GetBatch(PostageBatchId id, CancellationToken _) =>
                Task.FromResult(Batch(id, isUsable: false));

            // Run.
            var waitTask = PostageBatchUsablePoller.WaitUntilUsableAsync(
                PostageBatchId.Zero, GetBatch, timeout, PollInterval, fakeTimeProvider);
            await DriveVirtualClockUntilCompletedAsync(fakeTimeProvider, waitTask, PollInterval);

            // Assert.
            await Assert.ThrowsAsync<TimeoutException>(() => waitTask);
        }

        [Fact]
        public async Task ThrowsItsOwnTimeoutWhenEveryReadTimesOut()
        {
            // Setup.
            var fakeTimeProvider = new FakeTimeProvider();
            var timeout = TimeSpan.FromSeconds(20);
            var callCount = 0;
            Task<PostageBatch> GetBatch(PostageBatchId id, CancellationToken _)
            {
                callCount++;
                throw new TimeoutException("The node did not answer");
            }

            // Run.
            var waitTask = PostageBatchUsablePoller.WaitUntilUsableAsync(
                PostageBatchId.Zero, GetBatch, timeout, PollInterval, fakeTimeProvider);
            await DriveVirtualClockUntilCompletedAsync(fakeTimeProvider, waitTask, PollInterval);
            var thrown = await Assert.ThrowsAsync<TimeoutException>(() => waitTask);

            // Assert.
            Assert.StartsWith("Postage batch", thrown.Message, StringComparison.Ordinal);
            Assert.True(callCount > 1);
        }

        [Fact]
        public async Task ToleratesTransientNotFoundGatewayTimeoutAndReadTimeoutResponses()
        {
            // Setup.
            // A freshly bought batch may not be retrievable yet (404), the node may briefly time out (504), or a
            // read may miss its bound (TimeoutException): those transient answers must not abort the wait.
            var fakeTimeProvider = new FakeTimeProvider();
            var callCount = 0;
            Task<PostageBatch> GetBatch(PostageBatchId id, CancellationToken _)
            {
                callCount++;
                return callCount switch
                {
                    1 => throw ApiException(404),
                    2 => throw ApiException(504),
                    3 => throw new TimeoutException("The node did not answer"),
                    _ => Task.FromResult(Batch(id, isUsable: true))
                };
            }

            // Run.
            var waitTask = PostageBatchUsablePoller.WaitUntilUsableAsync(
                PostageBatchId.Zero, GetBatch, pollInterval: PollInterval, timeProvider: fakeTimeProvider);
            await DriveVirtualClockUntilCompletedAsync(fakeTimeProvider, waitTask, PollInterval);
            var result = await waitTask;

            // Assert.
            Assert.True(result.IsUsable);
            Assert.Equal(4, callCount);
        }

        [Fact]
        public async Task PropagatesNonTransientApiErrors()
        {
            // Setup.
            Task<PostageBatch> GetBatch(PostageBatchId id, CancellationToken _) =>
                throw ApiException(500);

            // Run & Assert.
            await Assert.ThrowsAsync<SwarmSdkApiException>(() =>
                PostageBatchUsablePoller.WaitUntilUsableAsync(
                    PostageBatchId.Zero, GetBatch, TimeSpan.FromMinutes(15), PollInterval, new FakeTimeProvider()));
        }

        [Fact]
        public async Task ThrowsOnNonPositivePollInterval()
        {
            // Setup.
            Task<PostageBatch> GetBatch(PostageBatchId id, CancellationToken _) =>
                Task.FromResult(Batch(id, isUsable: true));

            // Run & Assert.
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                PostageBatchUsablePoller.WaitUntilUsableAsync(
                    PostageBatchId.Zero, GetBatch, TimeSpan.FromMinutes(15), TimeSpan.Zero, new FakeTimeProvider()));
        }

        [Fact]
        public async Task ThrowsOnNegativeTimeout()
        {
            // Setup.
            Task<PostageBatch> GetBatch(PostageBatchId id, CancellationToken _) =>
                Task.FromResult(Batch(id, isUsable: true));

            // Run & Assert.
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                PostageBatchUsablePoller.WaitUntilUsableAsync(
                    PostageBatchId.Zero, GetBatch, TimeSpan.FromSeconds(-1), PollInterval, new FakeTimeProvider()));
        }

        // Helpers.
        private static SwarmSdkApiException ApiException(int statusCode) =>
            new("error", statusCode, null, new Dictionary<string, IEnumerable<string>>(), null);

        private static PostageBatch Batch(PostageBatchId id, bool isUsable) =>
            new(id: id,
                amount: null,
                blockNumber: 0,
                depth: PostageBatch.MinDepth,
                exists: true,
                isImmutable: false,
                isUsable: isUsable,
                label: null,
                ttl: TimeSpan.FromDays(1),
                utilization: 0);

        /// <summary>
        /// Repeatedly advances the virtual clock by <paramref name="step"/> until the polling task
        /// completes, settling the async continuations between advances. Bounded by wall-clock time
        /// so a stuck task cannot hang the test.
        /// </summary>
        private static async Task DriveVirtualClockUntilCompletedAsync(
            FakeTimeProvider timeProvider,
            Task task,
            TimeSpan step)
        {
            var realTimeout = Stopwatch.StartNew();
            while (!task.IsCompleted && realTimeout.Elapsed < TimeSpan.FromSeconds(5))
            {
                timeProvider.Advance(step);
                await Task.Delay(1);
            }
        }
    }
}
