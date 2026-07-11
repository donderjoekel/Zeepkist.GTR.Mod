using System;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostPlaybackInputService : IEagerService, IDisposable
{
    private const float SpeedStep = 0.1f;

    private readonly ConfigService _configService;
    private readonly GhostPlaybackService _playbackService;
    private readonly PhotoModeTimelineService _photoModeTimelineService;
    private readonly PlayerLoopService _playerLoopService;
    private readonly PlayerLoopSubscription _updateSubscription;
    private KeyRepeatTracker _speedIncreaseRepeat;
    private KeyRepeatTracker _speedDecreaseRepeat;

    public GhostPlaybackInputService(
        ConfigService configService,
        GhostPlaybackService playbackService,
        PhotoModeTimelineService photoModeTimelineService,
        PlayerLoopService playerLoopService)
    {
        _configService = configService;
        _playbackService = playbackService;
        _photoModeTimelineService = photoModeTimelineService;
        _playerLoopService = playerLoopService;
        _updateSubscription = _playerLoopService.SubscribeUpdate(OnUpdate);
    }

    private void OnUpdate()
    {
        if (!_photoModeTimelineService.IsTimelineAvailable)
            return;

        for (var i = 0; i < _configService.PlaybackScrubProgressKeys.Length; i++)
        {
            var key = _configService.PlaybackScrubProgressKeys[i].Value;
            if (key == KeyCode.None || !Input.GetKeyDown(key))
                continue;

            _playbackService.SeekToProgress(i * 0.1f);
        }

        TryAdjustSpeedOnRepeat(
            _configService.PlaybackSpeedIncreaseKey.Value,
            ref _speedIncreaseRepeat,
            SpeedStep);

        TryAdjustSpeedOnRepeat(
            _configService.PlaybackSpeedDecreaseKey.Value,
            ref _speedDecreaseRepeat,
            -SpeedStep);

        var resetKey = _configService.PlaybackSpeedResetKey.Value;
        if (resetKey != KeyCode.None && Input.GetKeyDown(resetKey))
            _playbackService.ResetSpeed();

        var toggleTimelineKey = _configService.ToggleShowTimeline.Value;
        if (toggleTimelineKey != KeyCode.None && Input.GetKeyDown(toggleTimelineKey))
            _configService.ShowTimeline.Value = !_configService.ShowTimeline.Value;

        var togglePlayPauseKey = _configService.TogglePlayPauseKey.Value;
        if (togglePlayPauseKey != KeyCode.None && Input.GetKeyDown(togglePlayPauseKey))
            _playbackService.TogglePlayPause();

        var previousFrameKey = _configService.PlaybackPreviousFrameKey.Value;
        if (previousFrameKey != KeyCode.None && Input.GetKeyDown(previousFrameKey))
            _playbackService.StepFrame(-1);

        var nextFrameKey = _configService.PlaybackNextFrameKey.Value;
        if (nextFrameKey != KeyCode.None && Input.GetKeyDown(nextFrameKey))
            _playbackService.StepFrame(1);
    }

    private void TryAdjustSpeedOnRepeat(KeyCode key, ref KeyRepeatTracker tracker, float delta)
    {
        if (key == KeyCode.None)
            return;

        if (!tracker.TryConsumeRepeat(
                Input.GetKey(key),
                Input.GetKeyDown(key),
                Time.unscaledDeltaTime))
            return;

        _playbackService.AdjustSpeed(delta);
    }

    public void Dispose()
    {
        _playerLoopService.UnsubscribeUpdate(_updateSubscription);
    }
}
