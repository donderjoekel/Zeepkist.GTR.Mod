using TNRD.Zeepkist.GTR.Ghosting.Playback;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class BulkGhostModeStateTests
{
    [Fact]
    public void RaisesChangeOnlyWhenStateChanges()
    {
        var state = new BulkGhostModeState();
        int changes = 0;
        state.Changed += () => changes++;

        state.SetActive(true);
        state.SetActive(true);
        state.SetActive(false);

        Assert.False(state.IsActive);
        Assert.Equal(2, changes);
    }
}
