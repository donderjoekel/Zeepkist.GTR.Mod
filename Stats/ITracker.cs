using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats;

internal interface ITracker
{
    void ApplyStats(UsersUpdateStatsRequestDTO stats);
    void Reset();
}
