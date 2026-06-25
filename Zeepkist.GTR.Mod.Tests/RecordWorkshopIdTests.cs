using Newtonsoft.Json;
using TNRD.Zeepkist.GTR.Api;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class RecordWorkshopIdTests
{
    [Fact]
    public void UsesDecimalStringForWorkshopLevel()
    {
        Assert.Equal(
            "18446744073709551615",
            RecordWorkshopId.ToWireValue(ulong.MaxValue, 0, false, false));
    }

    [Fact]
    public void UsesLobbyWorkshopIdBeforeClonedLevelValue()
    {
        Assert.Equal("3749321871", RecordWorkshopId.ToWireValue(3749321871, 0, false, false));
    }

    [Fact]
    public void FallsBackToLevelWorkshopId()
    {
        Assert.Equal("3749321871", RecordWorkshopId.ToWireValue(0, 3749321871, false, false));
    }

    [Theory]
    [InlineData(0, 0, false, false)]
    [InlineData(1, 1, true, false)]
    [InlineData(1, 1, false, true)]
    public void OmitsAdventureAndZeroWorkshopIds(
        ulong lobbyWorkshopId,
        ulong levelWorkshopId,
        bool adventure,
        bool avonturen)
    {
        Assert.Null(
            RecordWorkshopId.ToWireValue(
                lobbyWorkshopId,
                levelWorkshopId,
                adventure,
                avonturen));
    }

    [Fact]
    public void NullWorkshopIdIsOmittedFromJson()
    {
        RecordPostResource resource = new()
        {
            Level = "hash",
            Hash = "xxhash",
            WorkshopId = null
        };

        string json = JsonConvert.SerializeObject(resource);

        Assert.DoesNotContain("\"WorkshopId\"", json);
    }
}

