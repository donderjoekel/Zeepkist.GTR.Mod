using System;
using TNRD.Zeepkist.GTR.Core;
using UnityEngine;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public enum GhostPlaybackState
{
    Stopped,
    Playing,
    Paused
}

public class GhostPlaybackService : IEagerService
{
    private const float SkipSeconds = 5f;

    private readonly GhostTimingService _timingService;
    private readonly GhostPlayer _ghostPlayer;

    public GhostPlaybackState State { get; private set; } = GhostPlaybackState.Stopped;

    public float CurrentTime => _timingService.CurrentTime;
    public float Duration => _timingService.Duration;
    public float Speed => _timingService.Speed;
    public bool IsPlaying => State == GhostPlaybackState.Playing;

    public GhostPlaybackService(GhostTimingService timingService, GhostPlayer ghostPlayer)
    {
        _timingService = timingService;
        _ghostPlayer = ghostPlayer;

        _ghostPlayer.GhostAdded += OnGhostsChanged;
        _ghostPlayer.GhostRemoved += OnGhostsChanged;
        RacingApi.RoundStarted += OnRoundStarted;
        RefreshDuration();
    }

    private void OnRoundStarted()
    {
        Stop();
    }

    public void Play()
    {
        RefreshDuration();

        if (Duration <= 0f)
            return;

        if (State == GhostPlaybackState.Stopped)
        {
            var startTime = CurrentTime;
            _ghostPlayer.StartManualPlayback();
            _timingService.SetTime(startTime);
            _ghostPlayer.SeekAllGhosts(startTime);
        }
        else if (State == GhostPlaybackState.Paused)
        {
            _ghostPlayer.ResumeGhosts();
        }

        _timingService.StartManualPlayback();
        State = GhostPlaybackState.Playing;
    }

    public void Pause()
    {
        if (State != GhostPlaybackState.Playing)
            return;

        _timingService.PauseManualPlayback();
        _ghostPlayer.PauseGhosts();
        State = GhostPlaybackState.Paused;
    }

    public void Stop()
    {
        _timingService.StopManualPlayback();
        _ghostPlayer.StopManualPlayback();
        State = GhostPlaybackState.Stopped;
    }

    public void TogglePlayPause()
    {
        if (State == GhostPlaybackState.Playing)
            Pause();
        else
            Play();
    }

    public void Seek(float time)
    {
        RefreshDuration();
        float clampedTime = Duration > 0f ? Mathf.Clamp(time, 0f, Duration) : Mathf.Max(0f, time);
        _timingService.SetTime(clampedTime);
        _ghostPlayer.SeekAllGhosts(clampedTime);
    }

    public void SkipBackward()
    {
        Seek(CurrentTime - SkipSeconds);
    }

    public void SkipForward()
    {
        Seek(CurrentTime + SkipSeconds);
    }

    public void SetSpeed(float speed)
    {
        _timingService.SetSpeed(speed);
    }

    private void OnGhostsChanged(object sender, EventArgs e)
    {
        RefreshDuration();

        if (Duration > 0f && CurrentTime > Duration)
            Seek(Duration);
    }

    private void RefreshDuration()
    {
        _timingService.SetDuration(_ghostPlayer.GetMaxDuration());
    }
}
