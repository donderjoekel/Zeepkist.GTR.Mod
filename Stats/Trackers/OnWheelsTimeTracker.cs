using System.Collections.Generic;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class OnWheelsTimeTracker : RacingTimeTrackerBase
{
    private readonly Dictionary<int, float> wheelsToTime = new();

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.TimeOnOneWheel = GetTimeOnWheels(1);
        stats.TimeOnTwoWheels = GetTimeOnWheels(2);
        stats.TimeOnThreeWheels = GetTimeOnWheels(3);
        stats.TimeOnFourWheels = GetTimeOnWheels(4);
    }

    private float GetTimeOnWheels(int amountOfWheels)
    {
        if (wheelsToTime.TryGetValue(amountOfWheels, out float time))
            return time;
        return 0;
    }

    protected override void OnReset()
    {
        wheelsToTime.Clear();
    }

    protected override bool ShouldTrackTime()
    {
        return SetupCar.IsAnyWheelAlive() && SetupCar.IsAnyWheelGrounded();
    }

    protected override void OnTimeChanged(float delta, float total)
    {
        int amountOfWheelsGrounded = SetupCar.AmountOfWheelsGrounded();
        wheelsToTime.TryAdd(amountOfWheelsGrounded, 0);
        wheelsToTime[amountOfWheelsGrounded] += delta;
    }
}
