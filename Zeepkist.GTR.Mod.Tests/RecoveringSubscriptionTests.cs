using System.Threading.Channels;
using TNRD.Zeepkist.GTR.GraphQL;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class RecoveringSubscriptionTests
{
    private sealed class Disposable(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    [InlineData(5, 30)]
    [InlineData(100, 30)]
    public void RetryDelay_GrowsAndCaps(int failures, double seconds)
    {
        Assert.Equal(seconds, RecoveringSubscription<int>.RetryDelay(failures, 0.5).TotalSeconds);
        Assert.Equal(seconds * 0.8, RecoveringSubscription<int>.RetryDelay(failures, 0).TotalSeconds, 6);
        Assert.Equal(seconds * 1.2, RecoveringSubscription<int>.RetryDelay(failures, 1).TotalSeconds, 6);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoteCompletionOrError_DisposesAndRestartsExactlyOnce(bool error)
    {
        var attempts = Channel.CreateUnbounded<IObserver<int>>();
        var delays = Channel.CreateUnbounded<TaskCompletionSource>();
        var received = new List<int>();
        int active = 0;
        int disposed = 0;
        using var recovery = new RecoveringSubscription<int>(observer =>
        {
            Assert.Equal(1, Interlocked.Increment(ref active));
            attempts.Writer.TryWrite(observer);
            return new Disposable(() => { Interlocked.Decrement(ref active); Interlocked.Increment(ref disposed); });
        }, (value, _) => received.Add(value), _ => { },
            (_, token) =>
            {
                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                delays.Writer.TryWrite(gate);
                return gate.Task.WaitAsync(token);
            });
        recovery.Start();
        recovery.Start();
        IObserver<int> first = await attempts.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        first.OnNext(1);
        if (error) first.OnError(new IOException("connection lost"));
        else first.OnCompleted();
        first.OnNext(99);
        TaskCompletionSource delay = await delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, active);
        Assert.Equal(1, disposed);
        delay.SetResult();
        IObserver<int> second = await attempts.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        first.OnCompleted(); // Old transport cannot terminate new attempt.
        second.OnNext(2);
        Assert.Equal([1, 2], received);
        recovery.Dispose();
        await recovery.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, active);
        Assert.Equal(2, disposed);
        Assert.False(attempts.Reader.TryRead(out _));
    }

    [Fact]
    public async Task HealthySnapshot_ResetsBackoff_AndDisposeCancelsDelay()
    {
        var attempts = Channel.CreateUnbounded<IObserver<int>>();
        var delays = Channel.CreateUnbounded<(TimeSpan Delay, TaskCompletionSource Gate)>();
        RecoveringSubscription<int> recovery = null;
        recovery = new RecoveringSubscription<int>(observer =>
        {
            attempts.Writer.TryWrite(observer);
            return new Disposable(() => { });
        }, (_, attempt) => recovery.MarkHealthy(attempt), _ => { }, (delay, token) =>
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            delays.Writer.TryWrite((delay, gate));
            return gate.Task.WaitAsync(token);
        }, () => 0.5);
        using (recovery)
        {
            recovery.Start();
            for (int i = 0; i < 3; i++)
            {
                var observer = await attempts.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                if (i == 2) observer.OnNext(1);
                observer.OnCompleted();
                var delay = await delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(i == 1 ? 2 : 1, delay.Delay.TotalSeconds);
                if (i < 2) delay.Gate.SetResult();
            }
            recovery.Dispose();
            await recovery.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(attempts.Reader.TryRead(out _));
        }
    }

    [Fact]
    public async Task SynchronousSubscribeFailure_Retries()
    {
        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        using var recovery = new RecoveringSubscription<int>(observer =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new IOException();
            retried.TrySetResult();
            return new Disposable(() => { });
        }, (_, _) => { }, _ => { }, (_, _) => Task.CompletedTask);
        recovery.Start();
        await retried.Task.WaitAsync(TimeSpan.FromSeconds(5));
        recovery.Dispose();
        await recovery.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task TransportDisposeFailure_DoesNotStopRecovery()
    {
        var first = new TaskCompletionSource<IObserver<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0, errors = 0;
        using var recovery = new RecoveringSubscription<int>(observer =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                first.SetResult(observer);
                return new Disposable(() => throw new IOException("transport disposal failed"));
            }
            restored.SetResult();
            return new Disposable(() => { });
        }, (_, _) => { }, _ => Interlocked.Increment(ref errors), (_, _) => Task.CompletedTask);
        recovery.Start();
        (await first.Task.WaitAsync(TimeSpan.FromSeconds(5))).OnCompleted();
        await restored.Task.WaitAsync(TimeSpan.FromSeconds(5));
        recovery.Dispose();
        await recovery.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, calls);
        Assert.Equal(1, errors);
    }
}
