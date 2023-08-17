using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class InAirTimeTracker : RacingTimeTrackerBase
{
    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.TimeInAir = Time;
    }

    protected override bool ShouldTrackTime()
    {
        return SetupCar.AreAllWheelsInAir() && SetupCar.IsAnyWheelAlive();
    }
}
