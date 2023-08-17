using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class GroundedDistanceTracker : RacingDistanceTrackerBase
{
    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.DistanceGrounded = Distance;
    }

    protected override bool ShouldTrackDistance()
    {
        return SetupCar.IsAnyWheelGrounded() && SetupCar.IsAnyWheelAlive();
    }
}
