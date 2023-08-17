using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class OnWheelsDistanceTracker : RacingTrackerBase
{
    private readonly Dictionary<int, float> wheelsToDistance = new();
    private readonly Dictionary<int, bool> previousGrounded = new();

    private Vector3? previousPosition;

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.DistanceOnNoWheels = GetDistanceOnWheels(0);
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

    public override void Reset()
    {
        wheelsToDistance.Clear();
        previousGrounded.Clear();
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

        int amountGrounded = SetupCar.cc.GetWheels().Count(x => x.isGrounded);

        wheelsToDistance.TryAdd(amountGrounded, 0);

        if (previousGrounded.TryGetValue(amountGrounded, out bool wasGrounded) && wasGrounded)
        {
            wheelsToDistance[amountGrounded] += Vector3.Distance(previous.Value, current);
        }

        previousGrounded[amountGrounded] = true;
    }
}
