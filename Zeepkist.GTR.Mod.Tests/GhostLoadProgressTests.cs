using TNRD.Zeepkist.GTR.Ghosting.Playback;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostLoadProgressTests
{
    [Theory]
    [InlineData(0, 0, 100)]
    [InlineData(0, 100, 0)]
    [InlineData(25, 100, 25)]
    [InlineData(150, 100, 100)]
    public void CalculatesBoundedPercentage(int completed, int total, int expected)
    {
        Assert.Equal(expected, GhostLoadProgress.CalculatePercent(completed, total));
    }

    [Theory]
    [InlineData(4, 0, false)]
    [InlineData(5, 0, true)]
    [InlineData(19, 15, false)]
    [InlineData(20, 15, true)]
    [InlineData(100, 100, true)]
    public void ThrottlesProgressReports(int percent, int previous, bool expected)
    {
        Assert.Equal(expected, GhostLoadProgress.ShouldReport(percent, previous));
    }
}
