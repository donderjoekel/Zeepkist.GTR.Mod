using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class RagdollTimeTracker : RacingTimeTrackerBase
{
    protected override bool MustBeAlive => false;

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.TimeRagdoll = Time;
    }

    protected override bool ShouldTrackTime()
    {
        return SetupCar.characterDamage.IsDead();
    }
}
