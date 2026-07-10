using TNRD.Zeepkist.GTR.Ghosting.Playback;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class GhostCachePolicyTests
{
    [Fact]
    public void EvictsOldestEntriesUntilCacheFits()
    {
        GhostCacheEntry[] entries =
        {
            new() { RecordId = 1, Size = 40, LastAccess = 10 },
            new() { RecordId = 2, Size = 40, LastAccess = 20 },
            new() { RecordId = 3, Size = 40, LastAccess = 30 }
        };

        Assert.Equal(new[] { 1, 2 }, GhostCachePolicy.GetEvictionCandidates(entries, 50));
    }

    [Fact]
    public void KeepsEntriesWhenCacheFits()
    {
        GhostCacheEntry[] entries =
        {
            new() { RecordId = 1, Size = 40, LastAccess = 10 },
            new() { RecordId = 2, Size = 40, LastAccess = 20 }
        };

        Assert.Empty(GhostCachePolicy.GetEvictionCandidates(entries, 80));
    }
}
