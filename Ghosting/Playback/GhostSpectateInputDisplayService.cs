using System;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using ZeepSDK.PhotoMode;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostSpectateInputDisplayService : IEagerService, IDisposable
{
    private const float DefaultMaxSteerAngle = 20f;

    private readonly GhostSpectateService _spectateService;
    private readonly GhostPlayer _ghostPlayer;
    private readonly GhostTimingService _timingService;
    private readonly PlayerLoopService _playerLoopService;
    private readonly PlayerLoopSubscription _lateUpdateSubscription;

    private SpeedDisplay _speedDisplay;

    public GhostSpectateInputDisplayService(
        GhostSpectateService spectateService,
        GhostPlayer ghostPlayer,
        GhostTimingService timingService,
        PlayerLoopService playerLoopService)
    {
        _spectateService = spectateService;
        _ghostPlayer = ghostPlayer;
        _timingService = timingService;
        _playerLoopService = playerLoopService;

        _lateUpdateSubscription = playerLoopService.SubscribeLateUpdate(OnLateUpdate);
        PhotoModeApi.PhotoModeExited += OnPhotoModeExited;
    }

    private void OnPhotoModeExited()
    {
        _speedDisplay = null;
    }

    private void OnLateUpdate()
    {
        if (!_spectateService.IsActive)
            return;

        if (!TryGetSelectedGhost(out IGhost ghost))
        {
            HideDisplay();
            return;
        }

        if (ghost is not IGhostInputProvider inputProvider ||
            !inputProvider.TrySampleInputAtTime(_timingService.CurrentTime, out GhostInputSample sample))
        {
            HideDisplay();
            return;
        }

        if (!PhotoModeSpeedDisplay.TryGet(out SpeedDisplay speedDisplay))
            return;

        _speedDisplay = speedDisplay;
        _speedDisplay.supplyCustomValues = true;
        _speedDisplay.gameObject.SetActive(true);

        float maxSteerAngle = GetMaxSteerAngle();
        float steering = Mathf.Clamp(sample.Steering, -1f, 1f);
        _speedDisplay.DrawControlDisplay(
            sample.ArmsUp,
            sample.Braking,
            isActionKeys: true,
            inputAxis: steering,
            steeringAngle: steering,
            maxSteerAngle: maxSteerAngle,
            currentState: sample.ZeepkistState,
            sample.SpeedKmh);
    }

    private bool TryGetSelectedGhost(out IGhost ghost)
    {
        ghost = null;
        if (!_spectateService.SelectedRecordId.HasValue)
            return false;

        if (!_ghostPlayer.TryGetGhostData(_spectateService.SelectedRecordId.Value, out GhostData ghostData))
            return false;

        ghost = ghostData.Ghost;
        return ghost != null;
    }

    private static float GetMaxSteerAngle()
    {
        GameMaster master = PlayerManager.Instance?.currentMaster;
        if (master?.carSetups == null || master.carSetups.Count == 0)
            return DefaultMaxSteerAngle;

        return master.carSetups[0].steeringModuleLeft.maxSteerAngle;
    }

    private void HideDisplay()
    {
        if (_speedDisplay == null)
            return;

        _speedDisplay.gameObject.SetActive(false);
        _speedDisplay = null;
    }

    public void Dispose()
    {
        _playerLoopService.UnsubscribeLateUpdate(_lateUpdateSubscription);
        PhotoModeApi.PhotoModeExited -= OnPhotoModeExited;
    }
}
