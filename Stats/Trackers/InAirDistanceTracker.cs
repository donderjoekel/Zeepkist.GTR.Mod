using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class InAirDistanceTracker : RacingDistanceTrackerBase
{
    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.DistanceInAir = Distance;
    }

    protected override bool ShouldTrackDistance()
    {
        return SetupCar.AreAllWheelsInAir() && SetupCar.IsAnyWheelAlive();
    }
}
