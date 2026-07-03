using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class V6RagdollPlaybackStateTests
{
    [Fact]
    public void ResetShowsSeatedCharacterAndHidesRagdoll()
    {
        V6RagdollPlaybackState state = V6RagdollPlaybackState.Reset;

        Assert.True(state.SeatedCharacterVisible);
        Assert.False(state.RagdollVisible);
    }

    [Theory]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, false, true)]
    public void SegmentStateControlsSeatedAndRagdollVisibility(
        bool previousRagdoll,
        bool nextRagdoll,
        bool seatedCharacterVisible,
        bool ragdollVisible)
    {
        V6RagdollPlaybackState state = V6RagdollPlaybackState.FromSegment(
            previousRagdoll,
            nextRagdoll);

        Assert.Equal(seatedCharacterVisible, state.SeatedCharacterVisible);
        Assert.Equal(ragdollVisible, state.RagdollVisible);
    }
}
