using System.Collections.Concurrent;
using TNRD.Zeepkist.GTR.Utilities;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostLoadConcurrencyTests
{
    [Fact]
    public async Task SharedLoad_DeduplicatesCacheAndNetwork_OneCancelledWaiterDoesNotCancelOthers()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var complete = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        using var loads = new SharedAsyncLoad<int, object>(async (_, token) =>
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            return await complete.Task.WaitAsync(token);
        });
        using var cancelled = new CancellationTokenSource();
        Task<object> first = loads.GetAsync(7, cancelled.Token);
        Task<object> second = loads.GetAsync(7, CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        var data = new object();
        complete.SetResult(data);
        Assert.Same(data, await second.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task SharedLoad_LastCancelledWaiterCancelsWork_AndNewCallerCanRetry()
    {
        var abandoned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        using var loads = new SharedAsyncLoad<int, int>(async (_, token) =>
        {
            if (Interlocked.Increment(ref calls) > 1) return 42;
            started.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { abandoned.SetResult(); }
            return 0;
        });
        using var cancellation = new CancellationTokenSource();
        Task<int> first = loads.GetAsync(1, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await abandoned.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(42, await loads.GetAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task SharedLoad_FailureDoesNotPoisonLaterLoad()
    {
        int attempts = 0;
        using var loads = new SharedAsyncLoad<int, int>((_, _) =>
            Interlocked.Increment(ref attempts) == 1 ? Task.FromException<int>(new IOException()) : Task.FromResult(8));
        await Assert.ThrowsAsync<IOException>(() => loads.GetAsync(1, CancellationToken.None));
        Assert.Equal(8, await loads.GetAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task CreationQueue_BoundsPendingGhosts_AndCancelledGenerationReleasesSlots()
    {
        using var queue = new BoundedMainThreadQueue<int>(4);
        using var old = new CancellationTokenSource();
        for (int i = 0; i < 4; i++) await queue.EnqueueAsync(i, old.Token);
        Task blocked = queue.EnqueueAsync(99, old.Token);
        Assert.False(blocked.IsCompleted);
        old.Cancel();
        queue.DiscardCancelled();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked);
        await queue.EnqueueAsync(42, CancellationToken.None);
        Assert.True(queue.TryDequeue(out int value));
        Assert.Equal(42, value);
        Assert.False(queue.TryDequeue(out _));
    }

    [Fact]
    public async Task CreationQueue_DisposeCancelsBlockedProducer()
    {
        using var queue = new BoundedMainThreadQueue<object>(1);
        await queue.EnqueueAsync(new object(), CancellationToken.None);
        Task blocked = queue.EnqueueAsync(new object(), CancellationToken.None);
        queue.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked);
        Assert.False(queue.TryDequeue(out _));
    }

    [Fact]
    public async Task Preparation_ReservesCapacityBeforeDecode_AndCancellationReleasesReservation()
    {
        using var queue = new BoundedMainThreadQueue<int>(4);
        using var token = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int preparing = 0;
        var producers = Enumerable.Range(0, 20).Select(id => queue.EnqueuePreparedAsync(async cancellation =>
        {
            if (Interlocked.Increment(ref preparing) == 4) started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellation);
            return id;
        }, token.Token)).ToArray();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(4, Volatile.Read(ref preparing));
        token.Cancel();
        foreach (Task producer in producers)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => producer);
        await queue.EnqueueAsync(42, CancellationToken.None);
        Assert.True(queue.TryDequeue(out int value));
        Assert.Equal(42, value);
    }

    [Fact]
    public async Task BoundedWorkers_ProcessEveryRecordOnce_WithFixedConcurrency()
    {
        var items = Enumerable.Range(0, 1000).ToArray();
        var visited = new ConcurrentDictionary<int, byte>();
        int active = 0;
        int maximum = 0;
        await BoundedAsync.ForEachAsync(items, 15, async (record, token) =>
        {
            int count = Interlocked.Increment(ref active);
            lock (visited) maximum = Math.Max(maximum, count);
            await Task.Yield();
            Assert.True(visited.TryAdd(record, 0));
            Interlocked.Decrement(ref active);
        }, CancellationToken.None);
        Assert.InRange(maximum, 1, 15);
        Assert.Equal(1000, visited.Count);
    }
}
