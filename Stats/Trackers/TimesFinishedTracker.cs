using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class TimesFinishedTracker : TrackerBase
{
    private int timesFinished = 0;

    public TimesFinishedTracker()
    {
        RacingApi.CrossedFinishLine += OnCrossedFinishLine;
    }

    private void OnCrossedFinishLine(float time)
    {
        timesFinished++;
    }

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.TimesFinished = timesFinished;
    }

    public override void Reset()
    {
        timesFinished = 0;
    }
}
