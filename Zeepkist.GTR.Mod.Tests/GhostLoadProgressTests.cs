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
    [InlineData(0, 0, false)]
    [InlineData(1, 0, true)]
    [InlineData(5, 5, false)]
    [InlineData(6, 5, true)]
    public void ReportsOnlyAfterCompletionAdvances(
        int completed,
        int lastReportedCompleted,
        bool expected)
    {
        Assert.Equal(
            expected,
            GhostLoadProgress.HasAdvanced(completed, lastReportedCompleted));
    }
}
