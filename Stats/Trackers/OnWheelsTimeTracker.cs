using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class OnWheelsTimeTracker : RacingTrackerBase
{
    private readonly Dictionary<int, float> wheelsToTime = new();

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.TimeOnNoWheels = GetTimeOnWheels(0);
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

    public override void Reset()
    {
        wheelsToTime.Clear();
    }

    protected override void OnTick()
    {
        int amountGrounded = SetupCar.cc.GetWheels().Count(x => x.isGrounded);
        wheelsToTime.TryAdd(amountGrounded, 0);
        wheelsToTime[amountGrounded] += Time.deltaTime;
    }
}
