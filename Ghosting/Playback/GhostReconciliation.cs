using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public static class GhostReconciliation
{
    public static IReadOnlyList<int> GetObsoleteIds(
        IEnumerable<int> loadedIds,
        ISet<int> desiredIds)
    {
        var obsoleteIds = new List<int>();
        foreach (int loadedId in loadedIds)
        {
            if (!desiredIds.Contains(loadedId))
                obsoleteIds.Add(loadedId);
        }

        return obsoleteIds;
    }
}
