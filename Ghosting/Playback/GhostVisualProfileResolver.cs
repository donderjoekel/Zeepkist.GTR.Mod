using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public static class GhostVisualProfileResolver
{
    public static GhostVisualProfile Resolve(
        int recordId,
        ICollection<int> protectedGhostIds,
        ICollection<int> bulkGhostIds)
    {
        if (protectedGhostIds.Contains(recordId))
            return GhostVisualProfile.Full;

        return bulkGhostIds.Contains(recordId)
            ? GhostVisualProfile.Bulk
            : GhostVisualProfile.Full;
    }
}
