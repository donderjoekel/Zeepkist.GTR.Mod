using System.Collections.Generic;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class GroundTypeDistanceTracker : RacingDistanceTrackerBase
{
    private readonly Dictionary<GroundType, float> groundTypeToDistance = new();
    private GroundType? currentGroundType;

    protected override void OnReset()
    {
        currentGroundType = null;
        groundTypeToDistance[GroundType.Regular] = 0;
        groundTypeToDistance[GroundType.Grass] = 0;
        groundTypeToDistance[GroundType.Ice] = 0;
    }

    protected override void ResetValues()
    {
        currentGroundType = null;
    }

    protected override bool ShouldTrackDistance()
    {
        return GroundTypeUtility.Instance.TryGetGroundType(SetupCar, out currentGroundType);
    }

    protected override void OnDistanceChanged(float delta, float total)
    {
        if (!currentGroundType.HasValue)
            return;

        groundTypeToDistance[currentGroundType.Value] += delta;
    }

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.DistanceOnRegular = groundTypeToDistance[GroundType.Regular];
        stats.DistanceOnGrass = groundTypeToDistance[GroundType.Grass];
        stats.DistanceOnIce = groundTypeToDistance[GroundType.Ice];
    }
}
