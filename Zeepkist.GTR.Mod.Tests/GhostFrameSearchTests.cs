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
}
