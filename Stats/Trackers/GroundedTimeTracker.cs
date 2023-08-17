using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class GroundedTimeTracker : RacingTimeTrackerBase
{
    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.TimeGrounded = Time;
    }

    protected override bool ShouldTrackTime()
    {
        return SetupCar.IsAnyWheelGrounded() && SetupCar.IsAnyWheelAlive();
    }
}
