using TNRD.Zeepkist.GTR.Ghosting.Playback;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostReconciliationTests
{
    [Fact]
    public void RetainsUnchangedSameLevelGhosts()
    {
        IReadOnlyList<int> obsolete = GhostReconciliation.GetObsoleteIds(
            [1, 2, 3],
            new HashSet<int> { 1, 2, 3 });

        Assert.Empty(obsolete);
    }

    [Fact]
    public void RemovesOnlyRecordsNoLongerDesired()
    {
        IReadOnlyList<int> obsolete = GhostReconciliation.GetObsoleteIds(
            [1, 2, 3],
            new HashSet<int> { 1, 3, 4 });

        Assert.Equal([2], obsolete);
    }
}
