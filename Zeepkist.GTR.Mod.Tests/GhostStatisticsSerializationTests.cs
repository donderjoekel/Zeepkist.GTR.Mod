using Newtonsoft.Json;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostStatisticsSerializationTests
{
    [Fact]
    public void SerializesV6StatisticShape()
    {
        GhostStatistics statistics = new()
        {
            TimeInAir = 1,
            TimeOnGround = 2,
            TopSpeed = 500,
        };
        statistics.SurfaceDistance["sand"] = 3;
        statistics.SurfaceTime["sand"] = 4;

        string json = JsonConvert.SerializeObject(statistics);
        dynamic parsed = JsonConvert.DeserializeObject(json);

        Assert.Equal(1f, (float)parsed.timeInAir);
        Assert.Equal(2f, (float)parsed.timeOnGround);
        Assert.Equal(500f, (float)parsed.topSpeed);
        Assert.Equal(3f, (float)parsed.surfaceDistance.sand);
        Assert.Equal(4f, (float)parsed.surfaceTime.sand);
        Assert.DoesNotContain("topSpeedCapped", json);
        Assert.DoesNotContain("_sand", json);
    }
}
