using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class CheckpointsCrossedTracker : TrackerBase
{
    private int checkpointsCrossed = 0;

    public CheckpointsCrossedTracker()
    {
        RacingApi.PassedCheckpoint += OnPassedCheckpoint;
    }

    private void OnPassedCheckpoint(float time)
    {
        checkpointsCrossed++;
    }

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.CheckpointsCrossed = checkpointsCrossed;
    }

    public override void Reset()
    {
        checkpointsCrossed = 0;
    }
}
