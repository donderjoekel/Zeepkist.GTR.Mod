using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class TimesStartedTracker : TrackerBase
{
    private int timesStarted = 0;

    public TimesStartedTracker()
    {
        RacingApi.RoundStarted += OnRoundStarted;
    }

    private void OnRoundStarted()
    {
        timesStarted++;
    }

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.TimesStarted = timesStarted;
    }

    public override void Reset()
    {
        timesStarted = 0;
    }
}
