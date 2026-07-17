using System.Collections.Generic;
using System.Linq;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

internal sealed class GhostCacheIndex
{
    public List<GhostCacheEntry> Entries { get; set; } = new();
}

internal sealed class GhostCacheEntry
{
    public int RecordId { get; set; }
    public int Size { get; set; }
    public long LastAccess { get; set; }
}

internal static class GhostCachePolicy
{
    public static IReadOnlyList<int> GetEvictionCandidates(
        IEnumerable<GhostCacheEntry> entries,
        long maximumBytes)
    {
        List<GhostCacheEntry> oldestFirst = entries.OrderBy(entry => entry.LastAccess).ToList();
        long totalBytes = oldestFirst.Sum(entry => (long)entry.Size);
        List<int> result = new();

        foreach (GhostCacheEntry entry in oldestFirst)
        {
            if (totalBytes <= maximumBytes)
                break;

            totalBytes -= entry.Size;
            result.Add(entry.RecordId);
        }

        return result;
    }
}
