using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class V6CharacterPlaybackStateTests
{
    [Fact]
    public void ResetShowsNormalSeatedCharacterOnly()
    {
        V6CharacterPlaybackState state = V6CharacterPlaybackState.Reset;

        Assert.True(state.SeatedVisible);
        Assert.False(state.ArmsUpVisible);
        Assert.False(state.RagdollVisible);
    }

    [Theory]
    [InlineData(false, false, false, true, false, false)]
    [InlineData(false, false, true, false, true, false)]
    [InlineData(false, true, true, false, false, true)]
    [InlineData(true, true, false, false, false, true)]
    public void SegmentStateSelectsOneCharacterRenderGroup(
        bool previousRagdoll,
        bool nextRagdoll,
        bool armsUp,
        bool seatedVisible,
        bool armsUpVisible,
        bool ragdollVisible)
    {
        V6CharacterPlaybackState state = V6CharacterPlaybackState.FromSegment(
            previousRagdoll,
            nextRagdoll,
            armsUp);

        Assert.Equal(seatedVisible, state.SeatedVisible);
        Assert.Equal(armsUpVisible, state.ArmsUpVisible);
        Assert.Equal(ragdollVisible, state.RagdollVisible);
    }
}
