using System.Globalization;

namespace TNRD.Zeepkist.GTR.Api;

public static class RecordWorkshopId
{
    public static string ToWireValue(
        ulong lobbyWorkshopId,
        ulong levelWorkshopId,
        bool isAdventureLevel,
        bool useAvonturenLevel)
    {
        if (isAdventureLevel || useAvonturenLevel)
            return null;

        ulong workshopId = lobbyWorkshopId != 0 ? lobbyWorkshopId : levelWorkshopId;
        if (workshopId == 0)
            return null;

        return workshopId.ToString(CultureInfo.InvariantCulture);
    }
}
