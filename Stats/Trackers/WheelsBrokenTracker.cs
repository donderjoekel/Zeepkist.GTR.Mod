using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class WheelsBrokenTracker : TrackerBase
{
    private int wheelsBroken = 0;

    public WheelsBrokenTracker()
    {
        RacingApi.WheelBroken += OnWheelBroken;
    }

    private void OnWheelBroken()
    {
        wheelsBroken++;
    }

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.WheelsBroken = wheelsBroken;
    }

    public override void Reset()
    {
        wheelsBroken = 0;
    }
}
