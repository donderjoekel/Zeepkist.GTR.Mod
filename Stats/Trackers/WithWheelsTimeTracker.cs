using System.Collections.Generic;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class WithWheelsTimeTracker : RacingTrackerBase
{
    private readonly Dictionary<int, float> wheelsToTime = new();

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.TimeWithNoWheels = GetTimeOnWheels(0);
        stats.TimeWithOneWheel = GetTimeOnWheels(1);
        stats.TimeWithTwoWheels = GetTimeOnWheels(2);
        stats.TimeWithThreeWheels = GetTimeOnWheels(3);
        stats.TimeWithFourWheels = GetTimeOnWheels(4);
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
        int amountOfWheels = GetWheelsAlive();
        wheelsToTime.TryAdd(amountOfWheels, 0);
        wheelsToTime[amountOfWheels] += Time.deltaTime;
    }

    private int GetWheelsAlive()
    {
        int amount = 0;
        if (!SetupCar.damageLF.isdead)
            amount++;
        if (!SetupCar.damageRF.isdead)
            amount++;
        if (!SetupCar.damageLR.isdead)
            amount++;
        if (!SetupCar.damageRR.isdead)
            amount++;
        return amount;
    }
}
