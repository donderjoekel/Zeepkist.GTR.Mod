using System.Collections.Generic;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class WithWheelsDistanceTracker : RacingTrackerBase
{
    private readonly Dictionary<int, float> wheelsToDistance = new();

    private Vector3? previousPosition;

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.DistanceWithNoWheels = GetDistanceWithWheels(0);
        stats.DistanceWithOneWheel = GetDistanceWithWheels(1);
        stats.DistanceWithTwoWheels = GetDistanceWithWheels(2);
        stats.DistanceWithThreeWheels = GetDistanceWithWheels(3);
        stats.DistanceWithFourWheels = GetDistanceWithWheels(4);
    }

    private float GetDistanceWithWheels(int amountOfWheels)
    {
        if (wheelsToDistance.TryGetValue(amountOfWheels, out float distance))
            return distance;
        return 0;
    }

    public override void Reset()
    {
        wheelsToDistance.Clear();
    }

    protected override void OnTick()
    {
        Vector3? previous = previousPosition;
        Vector3 current = SetupCar.transform.position;
        previousPosition = current;

        if (SetupCar.cc.AreAllWheelsInAir())
            return;

        if (SetupCar.cc.GetLocalVelocity().magnitude < 0.1f) // Arbitrary value
            return;

        if (!previous.HasValue)
            return;

        int amountOfWheels = GetWheelsAlive();
        wheelsToDistance.TryAdd(amountOfWheels, 0);
        wheelsToDistance[amountOfWheels] += Vector3.Distance(previous.Value, current);
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
