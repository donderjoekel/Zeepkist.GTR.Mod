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

        var increaseKey = _configService.PlaybackSpeedIncreaseKey.Value;
        if (increaseKey != KeyCode.None && Input.GetKeyDown(increaseKey))
            _playbackService.AdjustSpeed(SpeedStep);

        var decreaseKey = _configService.PlaybackSpeedDecreaseKey.Value;
        if (decreaseKey != KeyCode.None && Input.GetKeyDown(decreaseKey))
            _playbackService.AdjustSpeed(-SpeedStep);

        var resetKey = _configService.PlaybackSpeedResetKey.Value;
        if (resetKey != KeyCode.None && Input.GetKeyDown(resetKey))
            _playbackService.ResetSpeed();

        var toggleTimelineKey = _configService.ToggleShowTimeline.Value;
        if (toggleTimelineKey != KeyCode.None && Input.GetKeyDown(toggleTimelineKey))
            _configService.ShowTimeline.Value = !_configService.ShowTimeline.Value;
    }

    public void Dispose()
    {
        _playerLoopService.UnsubscribeUpdate(_updateSubscription);
    }
}
