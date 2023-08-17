using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class BrakingTimeTracker : RacingTimeTrackerBase
{
    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.TimeBraking = Time;
    }

    protected override bool ShouldTrackTime()
    {
        return SetupCar.cc.BrakeAction.buttonHeld;
    }
}
