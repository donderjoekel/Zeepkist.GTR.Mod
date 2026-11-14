using TNRD.Zeepkist.GTR.Leaderboard;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class SharedLeaderboardPageStoreTests
{
    private const string FirstPage = "level|0|16";

    [Fact]
    public void OpeningUiStartsRequestBeforeTabSelectionAndReplaysCachedData()
    {
        using var pages = new SharedLeaderboardPageStore<int>();
        var source = new Source();
        using var prefetch = pages.Watch(FirstPage, source.Connect, _ => { }, Fail);
        Assert.Single(source.Streams);
        source.Streams[0].Push(42);
        var displayed = new List<int>();
        using var tab = pages.Watch(FirstPage, source.Connect, displayed.Add, Fail);
        Assert.Equal(new[] { 42 }, displayed);
        Assert.Single(source.Streams);
        source.Streams[0].Push(43);
        Assert.Equal(new[] { 42, 43 }, displayed);
    }

    [Fact]
    public void TabCanSubscribeBeforeUiOpenHookWithoutDuplicateRequest()
    {
        using var pages = new SharedLeaderboardPageStore<int>();
        var source = new Source();
        var displayed = new List<int>();
        using var tab = pages.Watch(FirstPage, source.Connect, displayed.Add, Fail);
        using var prefetch = pages.Watch(FirstPage, source.Connect, _ => { }, Fail);
        source.Streams[0].Push(1);
        Assert.Single(source.Streams);
        Assert.Equal(new[] { 1 }, displayed);
    }

    [Fact]
    public void SwitchingAwayKeepsFirstPageLiveAndReturningGetsLatestData()
    {
        using var pages = new SharedLeaderboardPageStore<int>();
        var source = new Source();
        using var prefetch = pages.Watch(FirstPage, source.Connect, _ => { }, Fail);
        var tab = pages.Watch(FirstPage, source.Connect, _ => { }, Fail);
        tab.Dispose();
        Assert.False(source.Streams[0].Disposed);
        source.Streams[0].Push(7);
        var displayed = new List<int>();
        using var nextTab = pages.Watch(FirstPage, source.Connect, displayed.Add, Fail);
        Assert.Single(source.Streams);
        Assert.Equal(new[] { 7 }, displayed);
    }

    [Fact]
    public void PaginationKeepsWarmFirstPageAndReleasesOtherPages()
    {
        using var pages = new SharedLeaderboardPageStore<int>();
        var source = new Source();
        using var prefetch = pages.Watch(FirstPage, source.Connect, _ => { }, Fail);
        var secondPage = pages.Watch("level|1|16", source.Connect, _ => { }, Fail);
        Assert.Equal(2, source.Streams.Count);
        secondPage.Dispose();
        Assert.False(source.Streams[0].Disposed);
        Assert.True(source.Streams[1].Disposed);
    }

    [Theory]
    [InlineData("other-level|0|16")]
    [InlineData("level|0|14")]
    public void LevelAndPageSizeKeepDataIsolated(string otherKey)
    {
        using var pages = new SharedLeaderboardPageStore<int>();
        var source = new Source();
        using var first = pages.Watch(FirstPage, source.Connect, _ => { }, Fail);
        source.Streams[0].Push(12);
        var displayed = new List<int>();
        using var other = pages.Watch(otherKey, source.Connect, displayed.Add, Fail);
        Assert.Empty(displayed);
        Assert.Equal(2, source.Streams.Count);
        source.Streams[1].Push(99);
        Assert.Equal(new[] { 99 }, displayed);
    }

    [Fact]
    public void CloseOrLevelChangeClearsCacheAndRejectsLateCallbacks()
    {
        var pages = new SharedLeaderboardPageStore<int>();
        var source = new Source();
        var displayed = new List<int>();
        var oldLease = pages.Watch(FirstPage, source.Connect, displayed.Add, Fail);
        source.Streams[0].Push(1);
        pages.Dispose();
        Assert.True(source.Streams[0].Disposed);
        using var newLease = pages.Watch(FirstPage, source.Connect, displayed.Add, Fail);
        source.Streams[0].Push(2);
        source.Streams[0].Error(new Exception("stale"));
        oldLease.Dispose();
        Assert.False(source.Streams[1].Disposed);
        Assert.Equal(new[] { 1 }, displayed);
        source.Streams[1].Push(3);
        Assert.Equal(new[] { 1, 3 }, displayed);
        pages.Dispose();
    }

    [Fact]
    public void ClosingUiReleasesEveryPageExactlyOnce()
    {
        var pages = new SharedLeaderboardPageStore<int>();
        var source = new Source();
        var prefetch = pages.Watch(FirstPage, source.Connect, _ => { }, Fail);
        var tab = pages.Watch(FirstPage, source.Connect, _ => { }, Fail);
        var second = pages.Watch("level|1|16", source.Connect, _ => { }, Fail);
        pages.Dispose();
        tab.Dispose();
        prefetch.Dispose();
        second.Dispose();
        pages.Dispose();
        Assert.All(source.Streams, stream => Assert.Equal(1, stream.DisposeCount));
    }

    [Fact]
    public void SynchronousInitialSnapshotIsNotLost()
    {
        using var pages = new SharedLeaderboardPageStore<int>();
        var source = new Source { InitialValue = 8 };
        var displayed = new List<int>();
        using var first = pages.Watch(FirstPage, source.Connect, displayed.Add, Fail);
        Assert.Equal(new[] { 8 }, displayed);
    }

    [Fact]
    public void BothSubscribersReceiveErrorsAndNextRecoverySnapshot()
    {
        using var pages = new SharedLeaderboardPageStore<int>();
        var source = new Source();
        var errors = new List<Exception>();
        var displayed = new List<int>();
        using var prefetch = pages.Watch(FirstPage, source.Connect, _ => { }, errors.Add);
        using var tab = pages.Watch(FirstPage, source.Connect, displayed.Add, errors.Add);
        var error = new Exception("transient");
        source.Streams[0].Error(error);
        Assert.Equal(new[] { error, error }, errors);
        source.Streams[0].Push(4);
        Assert.Equal(new[] { 4 }, displayed);
        Assert.Single(source.Streams);
    }

    private static void Fail(Exception error) => throw new Xunit.Sdk.XunitException(error.Message);

    private sealed class Source
    {
        public readonly List<Stream> Streams = new();
        public int? InitialValue;
        public IDisposable Connect(Action<int> next, Action<Exception> error)
        {
            var stream = new Stream(next, error);
            Streams.Add(stream);
            if (InitialValue.HasValue) next(InitialValue.Value);
            return stream;
        }
    }

    private sealed class Stream(Action<int> next, Action<Exception> error) : IDisposable
    {
        public int DisposeCount;
        public bool Disposed => DisposeCount > 0;
        public void Push(int value) => next(value);
        public void Error(Exception value) => error(value);
        public void Dispose() => DisposeCount++;
    }
}
