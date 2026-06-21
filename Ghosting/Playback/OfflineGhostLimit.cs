namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

internal static class OfflineGhostLimit
{
    public static int? ToGraphQlFirst(int configuredValue)
    {
        return configuredValue < 0 ? null : configuredValue;
    }
}
