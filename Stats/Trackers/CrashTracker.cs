using System.Collections.Generic;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Mod.Stats.Trackers;

internal class CrashTracker : TrackerBase
{
    private readonly Dictionary<CrashReason, int> crashes = new();

    public CrashTracker()
    {
        RacingApi.Crashed += OnCrashed;
    }

    private void OnCrashed(CrashReason reason)
    {
        crashes.TryAdd(reason, 0);
        crashes[reason]++;
    }

    public override void ApplyStats(UsersUpdateStatsRequestDTO stats)
    {
        stats.CrashEye = crashes.TryGetValue(CrashReason.Eye, out int eye) ? eye : 0;
        stats.CrashGhost = crashes.TryGetValue(CrashReason.Ghost, out int ghost) ? ghost : 0;
        stats.CrashSticky = crashes.TryGetValue(CrashReason.Sticky, out int sticky) ? sticky : 0;
        stats.CrashRegular = crashes.TryGetValue(CrashReason.Crashed, out int regular) ? regular : 0;
        stats.CrashTotal = stats.CrashEye + stats.CrashGhost + stats.CrashSticky + stats.CrashRegular;
    }

    public override void Reset()
    {
        crashes.Clear();
    }
}
