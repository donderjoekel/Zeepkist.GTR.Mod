using TNRD.Zeepkist.GTR.Ghosting.Playback;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostLoadBudgetTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(15, 1.99, true)]
    [InlineData(16, 0, false)]
    [InlineData(0, 2, false)]
    public void EnforcesCountAndTimeLimits(int processed, double elapsedMilliseconds, bool expected)
    {
        Assert.Equal(expected, GhostLoadBudget.CanProcessNext(processed, elapsedMilliseconds));
    }

    [Fact]
    public void RejectsStaleGeneration()
    {
        Assert.True(GhostLoadBudget.IsCurrentGeneration(4, 4));
        Assert.False(GhostLoadBudget.IsCurrentGeneration(3, 4));
    }
}
