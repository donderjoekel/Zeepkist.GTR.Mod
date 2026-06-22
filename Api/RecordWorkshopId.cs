using System.Globalization;

namespace TNRD.Zeepkist.GTR.Api;

public static class RecordWorkshopId
{
    public static string ToWireValue(ulong workshopId, bool isAdventureLevel, bool useAvonturenLevel)
    {
        if (workshopId == 0 || isAdventureLevel || useAvonturenLevel)
            return null;

        return workshopId.ToString(CultureInfo.InvariantCulture);
    }
}
