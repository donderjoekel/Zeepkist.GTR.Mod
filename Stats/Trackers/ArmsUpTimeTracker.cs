using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class ArmsUpTimeTracker : RacingTimeTrackerBase
{
    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.TimeArmsUp = Time;
    }

    protected override bool ShouldTrackTime()
    {
        return SetupCar.cc.ArmsUpAction.buttonHeld;
    }
}
