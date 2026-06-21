using TNRD.Zeepkist.GTR.Ghosting.Playback;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class BulkGhostMeshScaleTests
{
    [Theory]
    [InlineData(4, 1, 4)]
    [InlineData(2, 1, 2)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 0.97, 1)]
    public void CalculatesBoundsCorrection(float sourceSize, float bakedSize, float expected)
    {
        Assert.Equal(expected, BulkGhostMeshScale.CalculateUniformScale(sourceSize, bakedSize), 3);
    }
}
