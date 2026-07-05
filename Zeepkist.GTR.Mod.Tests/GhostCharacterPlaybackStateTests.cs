using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostCharacterPlaybackStateTests
{
    [Fact]
    public void Reset_IsSeated()
    {
        GhostCharacterPlaybackState state = GhostCharacterPlaybackState.Reset;

        Assert.Equal(GhostCharacterPlaybackPose.Seated, state.Pose);
        Assert.True(state.SeatedVisible);
        Assert.False(state.ArmsUpVisible);
        Assert.False(state.RagdollVisible);
    }

    [Theory]
    [InlineData(false, GhostCharacterPlaybackPose.Seated)]
    [InlineData(true, GhostCharacterPlaybackPose.SeatedArmsUp)]
    public void FromSeated_MapsArmsUp(bool armsUp, GhostCharacterPlaybackPose expected)
    {
        GhostCharacterPlaybackState state = GhostCharacterPlaybackState.FromSeated(armsUp);

        Assert.Equal(expected, state.Pose);
    }

    [Theory]
    [InlineData(false, false, false, GhostCharacterPlaybackPose.Seated)]
    [InlineData(false, false, true, GhostCharacterPlaybackPose.SeatedArmsUp)]
    [InlineData(true, false, true, GhostCharacterPlaybackPose.Ragdoll)]
    [InlineData(false, true, true, GhostCharacterPlaybackPose.Ragdoll)]
    [InlineData(true, true, false, GhostCharacterPlaybackPose.Ragdoll)]
    public void FromSegment_MapsPose(
        bool previousRagdoll,
        bool nextRagdoll,
        bool armsUp,
        GhostCharacterPlaybackPose expected)
    {
        GhostCharacterPlaybackState state = GhostCharacterPlaybackState.FromSegment(
            previousRagdoll,
            nextRagdoll,
            armsUp);

        Assert.Equal(expected, state.Pose);
    }
}