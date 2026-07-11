using TNRD.Zeepkist.GTR.Ghosting.Playback;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class KeyRepeatTrackerTests
{
    [Fact]
    public void FirstPressFiresImmediately()
    {
        var tracker = new KeyRepeatTracker();

        Assert.True(tracker.TryConsumeRepeat(isDown: true, isDownThisFrame: true, deltaTime: 0f));
    }

    [Fact]
    public void DoesNotRepeatBeforeInitialDelay()
    {
        var tracker = new KeyRepeatTracker();
        tracker.TryConsumeRepeat(isDown: true, isDownThisFrame: true, deltaTime: 0f);

        Assert.False(tracker.TryConsumeRepeat(
            isDown: true,
            isDownThisFrame: false,
            deltaTime: KeyRepeatTracker.InitialDelay - 0.01f));
    }

    [Fact]
    public void RepeatsAfterInitialDelay()
    {
        var tracker = new KeyRepeatTracker();
        tracker.TryConsumeRepeat(isDown: true, isDownThisFrame: true, deltaTime: 0f);

        Assert.True(tracker.TryConsumeRepeat(
            isDown: true,
            isDownThisFrame: false,
            deltaTime: KeyRepeatTracker.InitialDelay));
    }

    [Fact]
    public void RepeatsAtIntervalWhileHeld()
    {
        var tracker = new KeyRepeatTracker();
        tracker.TryConsumeRepeat(isDown: true, isDownThisFrame: true, deltaTime: 0f);
        tracker.TryConsumeRepeat(
            isDown: true,
            isDownThisFrame: false,
            deltaTime: KeyRepeatTracker.InitialDelay);

        Assert.False(tracker.TryConsumeRepeat(
            isDown: true,
            isDownThisFrame: false,
            deltaTime: KeyRepeatTracker.RepeatInterval - 0.01f));

        Assert.True(tracker.TryConsumeRepeat(
            isDown: true,
            isDownThisFrame: false,
            deltaTime: KeyRepeatTracker.RepeatInterval));
    }

    [Fact]
    public void ReleasingKeyResetsState()
    {
        var tracker = new KeyRepeatTracker();
        tracker.TryConsumeRepeat(isDown: true, isDownThisFrame: true, deltaTime: 0f);
        tracker.TryConsumeRepeat(
            isDown: true,
            isDownThisFrame: false,
            deltaTime: KeyRepeatTracker.InitialDelay);

        Assert.False(tracker.TryConsumeRepeat(isDown: false, isDownThisFrame: false, deltaTime: 0f));
        Assert.True(tracker.TryConsumeRepeat(isDown: true, isDownThisFrame: true, deltaTime: 0f));
    }

    [Fact]
    public void MultipleRepeatsWhileHeld()
    {
        var tracker = new KeyRepeatTracker();
        var repeatCount = 0;

        if (tracker.TryConsumeRepeat(isDown: true, isDownThisFrame: true, deltaTime: 0f))
            repeatCount++;

        var holdTime = 0f;
        while (holdTime < KeyRepeatTracker.InitialDelay + KeyRepeatTracker.RepeatInterval * 2f)
        {
            holdTime += KeyRepeatTracker.RepeatInterval;
            if (tracker.TryConsumeRepeat(isDown: true, isDownThisFrame: false, deltaTime: KeyRepeatTracker.RepeatInterval))
                repeatCount++;
        }

        Assert.Equal(4, repeatCount);
    }
}
