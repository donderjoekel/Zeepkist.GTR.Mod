using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostTimingService : IEagerService
{
    private bool _raceClockActive;
    private bool _manualPlaybackActive;
    private bool _manualPaused;
    private float _time;
    private float _speed = 1f;
    private float _duration;

    public float CurrentTime => _time;
    public float Speed => _speed;
    public float Duration => _duration;
    public bool IsManualPlaybackActive => _manualPlaybackActive;
    public bool IsManualPaused => _manualPaused;
    public bool IsManualPlaying => _manualPlaybackActive && !_manualPaused;

    public GhostTimingService(PlayerLoopService playerLoopService)
    {
        playerLoopService.SubscribeUpdate(Update);

        RacingApi.RoundStarted += OnRoundStarted;
        RacingApi.RoundEnded += OnRoundEnded;
        RacingApi.PlayerSpawned += OnPlayerSpawned;
        RacingApi.QuickReset += OnQuickReset;
    }

    public void SetDuration(float duration)
    {
        _duration = Mathf.Max(0f, duration);
        if (_duration > 0f && _time > _duration)
            _time = _duration;
    }

    public void StartManualPlayback()
    {
        _manualPlaybackActive = true;
        _manualPaused = false;
    }

    public void PauseManualPlayback()
    {
        _manualPaused = true;
    }

    public void ResumeManualPlayback()
    {
        _manualPaused = false;
    }

    public void StopManualPlayback()
    {
        _manualPlaybackActive = false;
        _manualPaused = false;
        _time = 0f;
    }

    public void SetTime(float time)
    {
        float maxTime = _duration > 0f ? _duration : float.MaxValue;
        _time = Mathf.Clamp(time, 0f, maxTime);
    }

    public void SetSpeed(float speed)
    {
        _speed = Mathf.Clamp(speed, 0.25f, 4f);
    }

    private void ResetRaceClock()
    {
        if (_manualPlaybackActive)
            return;

        _raceClockActive = false;
        _time = 0f;
    }

    private void OnRoundStarted()
    {
        ResetRaceClock();
        _raceClockActive = true;
    }

    private void OnRoundEnded()
    {
        ResetRaceClock();
    }

    private void OnPlayerSpawned()
    {
        ResetRaceClock();
    }

    private void OnQuickReset()
    {
        ResetRaceClock();
    }

    private void Update()
    {
        if (_manualPlaybackActive && !_manualPaused)
        {
            _time += Time.deltaTime * _speed;
            if (_duration > 0f && _time > _duration)
                _time = _duration;
            return;
        }

        if (!_raceClockActive)
            return;

        _time += Time.deltaTime;
    }
}
