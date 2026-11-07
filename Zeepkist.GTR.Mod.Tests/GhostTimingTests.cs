using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using ZeepSDK.PhotoMode;
using ZeepSDK.Racing;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

[Collection("Playback lifecycle")]
public class GhostTimingTests
{
    private sealed class Ghost : IGhost { public float Duration => 60; }

    private static (GhostTimingService Timing, GhostPlaybackService Playback) Create()
    {
        Time.deltaTime = .25f;
        Time.unscaledDeltaTime = .5f;
        var timing = new GhostTimingService();
        var player = new GhostPlayer();
        player.AddForTest(1, new Ghost(), new GhostData());
        var playback = new GhostPlaybackService(timing, player, new PlayerLoopService());
        RacingApi.StartRound();
        return (timing, playback);
    }

    [Fact]
    public void PauseDuringRace_FreezesClockAndResumeHasNoJump()
    {
        var (timing, playback) = Create();
        playback.Play();
        timing.Advance();
        Assert.Equal(.5f, timing.CurrentTime);
        playback.Pause();
        for (int i = 0; i < 100; i++) timing.Advance();
        Assert.Equal(.5f, timing.CurrentTime);
        Assert.Equal(GhostPlaybackState.Paused, playback.State);
        playback.Play();
        timing.Advance();
        Assert.Equal(1f, timing.CurrentTime);
    }

    [Fact]
    public void PhotoModeExit_PreservesPausedTimeDuringActiveRace()
    {
        var (timing, playback) = Create();
        var photoMode = new PhotoModeTimelineService(new GhostPlayer(), playback);
        PhotoModeApi.Enter();
        Assert.True(photoMode.IsPhotoModeActive);
        playback.Play();
        playback.Seek(12);
        PhotoModeApi.Exit();
        Assert.False(photoMode.IsPhotoModeActive);
        Assert.Equal(GhostPlaybackState.Paused, playback.State);
        timing.Advance();
        timing.Advance();
        Assert.Equal(12, timing.CurrentTime);
    }

    [Fact]
    public void RaceClockAndManualSpeed_KeepSeparateTimeSources()
    {
        var (timing, playback) = Create();
        timing.Advance();
        Assert.Equal(.25f, timing.CurrentTime);
        playback.Play();
        playback.SetSpeed(2);
        timing.Advance();
        Assert.Equal(1.25f, timing.CurrentTime);
        playback.Seek(59.5f);
        timing.Advance();
        Assert.Equal(60, timing.CurrentTime);
        playback.Stop();
        Assert.Equal(0, timing.CurrentTime);
        timing.Advance();
        Assert.Equal(.25f, timing.CurrentTime);
        RacingApi.EndRound();
        timing.Advance();
        Assert.Equal(0, timing.CurrentTime);
    }

    [Fact]
    public void RoundRestart_StopsManualPlaybackAndRestartsRaceClock()
    {
        var (timing, playback) = Create();
        playback.Play();
        playback.Seek(20);
        playback.Pause();
        RacingApi.StartRound();
        Assert.Equal(GhostPlaybackState.Stopped, playback.State);
        Assert.False(timing.IsManualPlaybackActive);
        Assert.Equal(0, timing.CurrentTime);
        timing.Advance();
        Assert.Equal(.25f, timing.CurrentTime);
    }
}
