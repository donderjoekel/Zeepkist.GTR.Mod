using TNRD.Zeepkist.GTR.Ghosting.Playback;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostVisualProfileResolverTests
{
    [Fact]
    public void BulkOnlyRecordUsesBulkVisuals()
    {
        GhostVisualProfile result = GhostVisualProfileResolver.Resolve(10, [], [10]);

        Assert.Equal(GhostVisualProfile.Bulk, result);
    }

    [Fact]
    public void ProtectedRecordUsesFullVisuals()
    {
        GhostVisualProfile result = GhostVisualProfileResolver.Resolve(10, [10], []);

        Assert.Equal(GhostVisualProfile.Full, result);
    }

    [Fact]
    public void ProtectedRecordWinsWhenAlsoBulk()
    {
        GhostVisualProfile result = GhostVisualProfileResolver.Resolve(10, [10], [10]);

        Assert.Equal(GhostVisualProfile.Full, result);
    }

    [Fact]
    public void RecordOutsideBulkSetUsesFullVisuals()
    {
        GhostVisualProfile result = GhostVisualProfileResolver.Resolve(10, [], []);

        Assert.Equal(GhostVisualProfile.Full, result);
    }
}
