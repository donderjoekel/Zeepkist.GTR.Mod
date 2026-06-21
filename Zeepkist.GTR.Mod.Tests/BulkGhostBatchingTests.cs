using TNRD.Zeepkist.GTR.Ghosting.Playback;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class BulkGhostBatchingTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(1023, 1)]
    [InlineData(1024, 2)]
    [InlineData(2046, 2)]
    [InlineData(2047, 3)]
    public void CalculatesRequiredDrawCalls(int instanceCount, int expected)
    {
        Assert.Equal(expected, BulkGhostBatching.GetBatchCount(instanceCount));
    }

    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(1000, 4, 4)]
    [InlineData(1024, 4, 8)]
    [InlineData(2000, 0, 0)]
    public void CalculatesDrawCallsAcrossMaterialGroups(
        int instanceCount,
        int materialCount,
        int expected)
    {
        Assert.Equal(expected, BulkGhostBatching.GetDrawCallCount(instanceCount, materialCount));
    }
}
