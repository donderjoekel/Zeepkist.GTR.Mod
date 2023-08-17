using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class BrakingDistanceTracker : RacingDistanceTrackerBase
{
    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.DistanceBraking = Distance;
    }

    protected override bool ShouldTrackDistance()
    {
        return SetupCar.cc.BrakeAction.buttonHeld;
    }
}
