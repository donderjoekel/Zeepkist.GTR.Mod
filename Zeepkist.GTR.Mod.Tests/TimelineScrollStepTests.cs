using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.UI.Timeline;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class TimelineScrollStepTests
{
    [Theory]
    [InlineData(false, false, false, false, 1f, 0)]
    [InlineData(false, true, false, false, 10f, 0)]
    [InlineData(false, false, true, false, 0.1f, 0)]
    [InlineData(true, false, false, true, 0f, 10)]
    [InlineData(true, true, false, true, 0f, 100)]
    [InlineData(true, false, true, true, 0f, 1)]
    [InlineData(false, true, true, false, 10f, 0)]
    [InlineData(true, true, true, true, 0f, 1)]
    public void GetScrubScrollStep_MapsModifiersToExpectedStep(
        bool alt,
        bool shift,
        bool control,
        bool expectedUseFrames,
        float expectedSeconds,
        int expectedFrameCount)
    {
        TimelineScrollStep.GetScrubScrollStep(alt, shift, control, out var useFrames, out var seconds, out var frameCount);

        Assert.Equal(expectedUseFrames, useFrames);
        Assert.Equal(expectedSeconds, seconds);
        Assert.Equal(expectedFrameCount, frameCount);
    }

    [Fact]
    public void StepFramesLogic_AdvancesTenFramesForwardFromMidSegment()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f, 16f, 32f, 64f, 128f, 256f, 512f };
        const float epsilon = 0.005f;

        Assert.True(TryStepFrames(frameTimes, 1.5f, 1, 10, epsilon, out float forwardTime));
        Assert.Equal(512f, forwardTime);
    }

    [Fact]
    public void StepFramesLogic_AdvancesTenFramesBackwardFromMidSegment()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f, 16f, 32f, 64f, 128f, 256f, 512f };
        const float epsilon = 0.005f;

        Assert.True(TryStepFrames(frameTimes, 300f, -1, 10, epsilon, out float backwardTime));
        Assert.Equal(0f, backwardTime);
    }

    private static bool TryStepFrames(
        float[] frameTimes,
        float currentTime,
        int direction,
        int frameCount,
        float timeEpsilon,
        out float newTime)
    {
        newTime = currentTime;
        var stepped = false;
        for (var i = 0; i < frameCount; i++)
        {
            if (!GhostFrameSearch.TryGetAdjacentFrameTime(
                    frameTimes.Length,
                    newTime,
                    direction,
                    timeEpsilon,
                    index => frameTimes[index],
                    out float steppedTime))
                break;

            newTime = steppedTime;
            stepped = true;
        }

        return stepped;
    }
}
