using System;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public static class GhostLoadProgress
{
    public static int CalculatePercent(int completed, int total)
    {
        if (total <= 0)
            return 100;

        return Math.Min(100, Math.Max(0, completed * 100 / total));
    }

    public static bool HasAdvanced(int completed, int lastReportedCompleted)
    {
        return completed > lastReportedCompleted;
    }
}
