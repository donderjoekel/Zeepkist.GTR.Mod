using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostFrameSearchTests
{
    [Fact]
    public void FindFirstFrameIndexAtOrAfterTime_ReturnsFirstMatchingFrame()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };

        Assert.Equal(1, GhostFrameSearch.FindFirstFrameIndexAtOrAfterTime(frameTimes.Length, 0.5f, i => frameTimes[i]));
        Assert.Equal(2, GhostFrameSearch.FindFirstFrameIndexAtOrAfterTime(frameTimes.Length, 2f, i => frameTimes[i]));
        Assert.Equal(3, GhostFrameSearch.FindFirstFrameIndexAtOrAfterTime(frameTimes.Length, 3f, i => frameTimes[i]));
        Assert.Equal(4, GhostFrameSearch.FindFirstFrameIndexAtOrAfterTime(frameTimes.Length, 8f, i => frameTimes[i]));
    }

    [Fact]
    public void FindFirstFrameIndexAtOrAfterTime_WorksWithFrameList()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };

        Assert.Equal(3, GhostFrameSearch.FindFirstFrameIndexAtOrAfterTime(frameTimes, 3.5f, time => time));
    }

    [Fact]
    public void TryGetAdjacentFrameTime_StepsForwardFromMidSegment()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };
        const float epsilon = 0.005f;

        Assert.True(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            1.5f,
            1,
            epsilon,
            i => frameTimes[i],
            out float forwardTime));
        Assert.Equal(2f, forwardTime);
    }

    [Fact]
    public void TryGetAdjacentFrameTime_StepsBackwardFromMidSegment()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };
        const float epsilon = 0.005f;

        Assert.True(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            1.5f,
            -1,
            epsilon,
            i => frameTimes[i],
            out float backwardTime));
        Assert.Equal(1f, backwardTime);
    }

    [Fact]
    public void TryGetAdjacentFrameTime_StepsForwardFromExactFrameBoundary()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };
        const float epsilon = 0.005f;

        Assert.True(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            2f,
            1,
            epsilon,
            i => frameTimes[i],
            out float forwardTime));
        Assert.Equal(4f, forwardTime);
    }

    [Fact]
    public void TryGetAdjacentFrameTime_StepsBackwardFromExactFrameBoundary()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };
        const float epsilon = 0.005f;

        Assert.True(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            2f,
            -1,
            epsilon,
            i => frameTimes[i],
            out float backwardTime));
        Assert.Equal(1f, backwardTime);
    }

    [Fact]
    public void TryGetAdjacentFrameTime_ReturnsFalseAtStartAndEnd()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };
        const float epsilon = 0.005f;

        Assert.False(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            0f,
            -1,
            epsilon,
            i => frameTimes[i],
            out _));

        Assert.False(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            8f,
            1,
            epsilon,
            i => frameTimes[i],
            out _));
    }
}
