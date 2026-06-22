using Newtonsoft.Json;
using TNRD.Zeepkist.GTR.Api;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class RecordWorkshopIdTests
{
    [Fact]
    public void UsesDecimalStringForWorkshopLevel()
    {
        Assert.Equal("18446744073709551615", RecordWorkshopId.ToWireValue(ulong.MaxValue, false, false));
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, true, false)]
    [InlineData(1, false, true)]
    public void OmitsAdventureAndZeroWorkshopIds(ulong workshopId, bool adventure, bool avonturen)
    {
        Assert.Null(RecordWorkshopId.ToWireValue(workshopId, adventure, avonturen));
    }

    [Fact]
    public void NullWorkshopIdIsOmittedFromJson()
    {
        RecordPostResource resource = new()
        {
            Level = "hash",
            WorkshopId = null
        };

        string json = JsonConvert.SerializeObject(resource);

        Assert.DoesNotContain("\"WorkshopId\"", json);
    }
}
