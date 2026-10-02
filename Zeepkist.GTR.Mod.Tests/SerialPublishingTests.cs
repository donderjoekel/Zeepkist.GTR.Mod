using System.Collections.Concurrent;
using TNRD.Zeepkist.GTR.Utilities;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class SerialPublishingTests
{
    private sealed class Recording(int id) : IDisposable
    {
        public int Id { get; } = id;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task RapidFinishes_StayFifo_WithOneEncoderAndOneUpload_AndReleaseFrames()
    {
        var inputs = Enumerable.Range(0, 50).Select(id => new Recording(id)).ToArray();
        var encoded = new ConcurrentQueue<int>();
        var uploaded = new ConcurrentQueue<int>();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int encoding = 0, uploading = 0;
        using var pipeline = new SerialPipeline<Recording, int>(async (recording, _) =>
        {
            Assert.Equal(1, Interlocked.Increment(ref encoding));
            await Task.Yield();
            encoded.Enqueue(recording.Id);
            Interlocked.Decrement(ref encoding);
            return recording.Id;
        }, async (id, _) =>
        {
            Assert.Equal(1, Interlocked.Increment(ref uploading));
            await Task.Yield();
            uploaded.Enqueue(id);
            Interlocked.Decrement(ref uploading);
            if (id == 49) done.SetResult();
        }, done.SetException);
        foreach (var input in inputs) pipeline.Enqueue(input);
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pipeline.Dispose();
        await pipeline.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Enumerable.Range(0, 50), encoded);
        Assert.Equal(Enumerable.Range(0, 50), uploaded);
        Assert.All(inputs, input => Assert.True(input.Disposed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EncodeOrUploadFailure_DoesNotStopLaterRuns(bool uploadFailure)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var errors = new ConcurrentQueue<Exception>();
        using var pipeline = new SerialPipeline<Recording, int>((input, _) =>
        {
            if (input.Id == 1 && !uploadFailure) throw new IOException();
            return Task.FromResult(input.Id);
        }, (id, _) =>
        {
            if (id == 1 && uploadFailure) throw new IOException();
            if (id == 2) done.SetResult();
            return Task.CompletedTask;
        }, errors.Enqueue);
        for (int i = 0; i < 3; i++) pipeline.Enqueue(new Recording(i));
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pipeline.Dispose();
        await pipeline.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(errors);
    }

    [Fact]
    public async Task Shutdown_ReleasesQueuedRecordingsAndCancelsUpload()
    {
        var uploading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recordings = Enumerable.Range(0, 100).Select(id => new Recording(id)).ToArray();
        using var pipeline = new SerialPipeline<Recording, int>(async (input, token) =>
        {
            if (input.Id > 0) await Task.Delay(Timeout.Infinite, token);
            return input.Id;
        }, async (_, token) =>
        {
            uploading.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        }, _ => { });
        foreach (var input in recordings) pipeline.Enqueue(input);
        await uploading.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pipeline.Dispose();
        await pipeline.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.All(recordings, input => Assert.True(input.Disposed));
    }
}
