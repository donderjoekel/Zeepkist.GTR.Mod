using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class RagdollDistanceTracker : RacingDistanceTrackerBase
{
    protected override bool MustBeAlive => false;

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.DistanceRagdoll = Distance;
    }

    protected override bool ShouldTrackDistance()
    {
        return SetupCar.characterDamage.IsDead();
    }

    protected override Vector3 GetPosition()
    {
        return SetupCar.characterDamage.rb.position;
    }
}
