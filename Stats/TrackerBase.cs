using BepInEx.Logging;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Mod.Stats;

internal abstract class TrackerBase : ITracker
{
    protected static ManualLogSource Logger { get; private set; }

    protected TrackerBase()
    {
        Logger = BepInEx.Logging.Logger.CreateLogSource(GetType().Name);
    }

    public abstract void ApplyStats(UsersUpdateStatsRequestDTO stats);
    public abstract void Reset();
}
