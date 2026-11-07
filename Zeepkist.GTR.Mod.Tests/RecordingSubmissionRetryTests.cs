using System.Net;
using TNRD.Zeepkist.GTR.Connectivity;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using TNRD.Zeepkist.GTR.Utilities;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class RecordingSubmissionRetryTests
{
    [Fact]
    public async Task TimeoutThenSuccess_ReusesBodyAndBackoff()
    {
        const string json = "{\"runUuid\":\"unchanged\",\"ghost\":\"AQID\"}";
        var bodies = new List<string>();
        var delays = new List<int>();
        await RecordingSubmissionRetry.UploadAsync(json, (body, _) =>
        {
            bodies.Add(body);
            return bodies.Count == 1
                ? Task.FromException<HttpResponseMessage>(new TaskCanceledException())
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }, CancellationToken.None, (milliseconds, _) => { delays.Add(milliseconds); return Task.CompletedTask; });
        Assert.Equal(new[] { json, json }, bodies);
        Assert.Equal(new[] { 500 }, delays);
    }

    [Fact]
    public async Task ExhaustedTimeouts_ReportOnceAndAllowNextRecording()
    {
        int attempts = 0;
        var errors = new List<Exception>();
        var delays = new List<int>();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var pipeline = new SerialPipeline<int, int>(
            (input, _) => Task.FromResult(input),
            async (input, token) =>
            {
                if (input == 2) { done.SetResult(); return; }
                await RecordingSubmissionRetry.UploadAsync("{}", (_, _) =>
                {
                    attempts++;
                    throw new TaskCanceledException();
                }, token, (milliseconds, _) => { delays.Add(milliseconds); return Task.CompletedTask; });
            }, errors.Add);
        pipeline.Enqueue(1);
        pipeline.Enqueue(2);
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pipeline.Dispose();
        await pipeline.Completion;
        Assert.Equal(4, attempts);
        Assert.Equal(new[] { 500, 1000, 2000 }, delays);
        Assert.IsType<TaskCanceledException>(Assert.Single(errors));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShutdownDuringRequestOrBackoff_DoesNotRetry(bool duringBackoff)
    {
        using var cancellation = new CancellationTokenSource();
        int attempts = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RecordingSubmissionRetry.UploadAsync("{}", (_, token) =>
        {
            attempts++;
            if (!duringBackoff) cancellation.Cancel();
            throw new OperationCanceledException();
        }, cancellation.Token, (_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }));
        Assert.Equal(1, attempts);
    }

    private sealed class TrackedContent : StringContent
    {
        public bool Disposed { get; private set; }
        public TrackedContent() : base("unavailable") { }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    [Fact]
    public async Task RetryableResponse_IsDisposedBeforeDelay()
    {
        var content = new TrackedContent();
        int attempts = 0;
        await RecordingSubmissionRetry.UploadAsync("{}", (_, _) => Task.FromResult(
            ++attempts == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = content }
                : new HttpResponseMessage(HttpStatusCode.OK)), CancellationToken.None,
            (_, _) => { Assert.True(content.Disposed); return Task.CompletedTask; });
        Assert.Equal(2, attempts);
    }

    [Theory]
    [InlineData("https://backend.invalid/record/submit", 120)]
    [InlineData("https://alternative.invalid/record/submit", 120)]
    [InlineData("https://backend.invalid/auth/login", 10)]
    [InlineData("https://backend.invalid/vote/submit", 10)]
    [InlineData("https://backend.invalid/graphql", 10)]
    public void Timeout_IsLongerOnlyForRecording(string url, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), AlternativeDomainFallbackHandler.GetRequestTimeout(new Uri(url)));
}
