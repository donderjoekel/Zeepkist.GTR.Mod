using TNRD.Zeepkist.GTR.UI.Timeline;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostTimelineStateTests
{
    [Fact]
    public void ShouldShow_IsFalseWhenVisibleButHiddenByOverlay()
    {
        var state = new GhostTimelineState();
        state.SetVisible(true);
        state.SetHiddenByOverlay(true);

        Assert.False(state.ShouldShow);
    }

    [Fact]
    public void ShouldShow_IsTrueWhenVisibleAndOverlayIsInactive()
    {
        var state = new GhostTimelineState();
        state.SetVisible(true);
        state.SetHiddenByOverlay(false);

        Assert.True(state.ShouldShow);
    }

    [Fact]
    public void ShouldShow_IsFalseWhenUserHidesTimelineEvenIfOverlayIsInactive()
    {
        var state = new GhostTimelineState();
        state.SetVisible(false);
        state.SetHiddenByOverlay(false);

        Assert.False(state.ShouldShow);
    }

    [Fact]
    public void ShouldShow_IsFalseWhenUserHidesTimelineAndOverlayIsActive()
    {
        var state = new GhostTimelineState();
        state.SetVisible(false);
        state.SetHiddenByOverlay(true);

        Assert.False(state.ShouldShow);
    }
}
