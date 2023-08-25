using System.Collections.Generic;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class OnWheelsDistanceTracker : RacingDistanceTrackerBase
{
    private readonly Dictionary<int, float> wheelsToDistance = new();

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.DistanceOnOneWheel = GetDistanceOnWheels(1);
        stats.DistanceOnTwoWheels = GetDistanceOnWheels(2);
        stats.DistanceOnThreeWheels = GetDistanceOnWheels(3);
        stats.DistanceOnFourWheels = GetDistanceOnWheels(4);
    }

    private float GetDistanceOnWheels(int amountOfWheels)
    {
        if (wheelsToDistance.TryGetValue(amountOfWheels, out float distance))
            return distance;
        return 0;
    }

    protected override void OnReset()
    {
        wheelsToDistance.Clear();
    }

    protected override bool ShouldTrackDistance()
    {
        return SetupCar.IsAnyWheelAlive() && SetupCar.IsAnyWheelGrounded();
    }

    protected override void OnDistanceChanged(float delta, float total)
    {
        int amountOfWheelsGrounded = SetupCar.AmountOfWheelsGrounded();
        wheelsToDistance.TryAdd(amountOfWheelsGrounded, 0);
        wheelsToDistance[amountOfWheelsGrounded] += delta;
    }
}
