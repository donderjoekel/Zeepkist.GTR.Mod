using TNRD.Zeepkist.GTR.Core;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class GtrVersionTests
{
    [Theory]
    [InlineData("1.9.0", "1.10.0", true)]
    [InlineData("1.10.0", "1.9.0", false)]
    [InlineData("1.10", "1.10.0.0", false)]
    [InlineData("1", "1.0", false)]
    [InlineData("1.10.0.1", "1.10.0.2", true)]
    [InlineData("2.0.0", "1.9.0", false)]
    public void MinimumControlsSupport(string current, string minimum, bool unsupported)
    {
        Assert.Equal(unsupported, GtrVersionStatus.Evaluate(current, minimum, "1.9.0").IsUnsupported);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1..0")]
    [InlineData("-1.0")]
    [InlineData("1.2.3-beta")]
    [InlineData("1.2.3.4.5")]
    [InlineData("2147483648.0")]
    public void InvalidVersionsNeverClaimUnsupported(string invalid)
    {
        Assert.Throws<FormatException>(() => GtrVersionStatus.Evaluate("1.0", invalid, "2.0"));
        Assert.Throws<FormatException>(() => GtrVersionStatus.Evaluate(invalid, "2.0", "2.0"));
        var result = GtrVersionStatus.Evaluate("1.0", "2.0", invalid);
        Assert.True(result.IsUnsupported);
        Assert.Null(result.Latest);
        Assert.Equal("Update GTR", result.UpdateMessage);
    }

    [Fact]
    public void UpdateMessageUsesLatestExactly()
    {
        var result = GtrVersionStatus.Evaluate("1.0", "2.0", "2.4.0");
        Assert.Equal("Update GTR to 2.4.0", result.UpdateMessage);
        Assert.Equal("1.0", result.Current);
        Assert.Equal("2.0", result.Minimum);
    }

    [Fact]
    public async Task CheckWaitsForTraceAndCompletesOnlyAfterVersionDecision()
    {
        var trace = new TaskCompletionSource();
        var check = new TaskCompletionSource<GtrVersionStatus>();
        int requests = 0;
        Task<GtrVersionStatus> pending = GtrStartupGate.CheckAsync("1.0", () => trace.Task,
            _ => { requests++; return check.Task; }, _ => Assert.Fail("Unexpected failure"), default);
        Assert.Equal(0, requests);
        Assert.False(pending.IsCompleted);
        trace.SetResult();
        await Task.Yield();
        Assert.False(pending.IsCompleted);
        check.SetResult(GtrVersionStatus.Evaluate("1.0", "2.0", "2.1"));
        Assert.True((await pending).IsUnsupported);
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task FailedCheckAllowsNormalStartupWithoutRetry()
    {
        int requests = 0;
        int failures = 0;
        var result = await GtrStartupGate.CheckAsync("1.0", () => Task.CompletedTask,
            _ => { requests++; throw new HttpRequestException("offline"); }, _ => failures++, default);
        Assert.False(result.IsUnsupported);
        Assert.Equal(1, requests);
        Assert.Equal(1, failures);
    }

    [Fact]
    public async Task UnloadDuringTracePreventsVersionRequestAndStartup()
    {
        using var cancellation = new CancellationTokenSource();
        var trace = new TaskCompletionSource();
        int requests = 0;
        var pending = GtrStartupGate.CheckAsync("1.0", () => trace.Task,
            _ => { requests++; return Task.FromResult(GtrVersionStatus.Unknown("1.0")); },
            _ => Assert.Fail("Unload must not fail open"), cancellation.Token);
        cancellation.Cancel();
        trace.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(0, requests);
    }
}
