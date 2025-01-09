using System.Linq;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostTimingService : IEagerService
{
    private bool _started;
    private float _time;

    public float CurrentTime => _time;

    public GhostTimingService(PlayerLoopService playerLoopService)
    {
        playerLoopService.SubscribeUpdate(Update);

        RacingApi.RoundStarted += OnRoundStarted;
        RacingApi.RoundEnded += OnRoundEnded;
        RacingApi.PlayerSpawned += OnPlayerSpawned;
        RacingApi.QuickReset += OnQuickReset;
    }

    private void Reset()
    {
        _started = false;
        _time = 0;
    }

    private void OnRoundStarted()
    {
        Reset();
        _started = true;
    }

    private void OnRoundEnded()
    {
        Reset();
    }

    private void OnPlayerSpawned()
    {
        Reset();
    }

    private void OnQuickReset()
    {
        Reset();
    }

    private void Update()
    {
        if (!_started)
            return;

        _time += Time.deltaTime;
    }
}
