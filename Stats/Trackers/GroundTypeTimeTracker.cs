using System.Collections.Generic;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class GroundTypeTimeTracker : RacingTimeTrackerBase
{
    private readonly Dictionary<GroundType, float> groundTypeToTime = new();
    private GroundType? currentGroundType;

    protected override void OnReset()
    {
        currentGroundType = null;
        groundTypeToTime[GroundType.Regular] = 0;
        groundTypeToTime[GroundType.Grass] = 0;
        groundTypeToTime[GroundType.Ice] = 0;
    }

    protected override void ResetValues()
    {
        currentGroundType = null;
    }

    protected override bool ShouldTrackTime()
    {
        return GroundTypeUtility.Instance.TryGetGroundType(SetupCar, out currentGroundType);
    }

    protected override void OnTimeChanged(float delta, float total)
    {
        if (!currentGroundType.HasValue)
            return;

        groundTypeToTime[currentGroundType.Value] += delta;
    }

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.TimeOnRegular = groundTypeToTime[GroundType.Regular];
        stats.TimeOnGrass = groundTypeToTime[GroundType.Grass];
        stats.TimeOnIce = groundTypeToTime[GroundType.Ice];
    }
}
