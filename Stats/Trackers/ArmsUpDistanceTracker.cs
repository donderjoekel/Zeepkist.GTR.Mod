using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class ArmsUpDistanceTracker : RacingDistanceTrackerBase
{
    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.DistanceArmsUp = Distance;
    }

    protected override bool ShouldTrackDistance()
    {
        return SetupCar.cc.ArmsUpAction.buttonHeld;
    }
}
