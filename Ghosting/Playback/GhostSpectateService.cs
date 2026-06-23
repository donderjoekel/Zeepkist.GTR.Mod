using System;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using ZeepSDK.PhotoMode;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostSpectateService : IEagerService, IDisposable
{
    private readonly ConfigService _configService;
    private readonly GhostPlayer _ghostPlayer;
    private readonly PhotoModeTimelineService _photoModeTimelineService;
    private readonly PlayerLoopService _playerLoopService;
    private readonly PlayerLoopSubscription _lateUpdateSubscription;

    private Vector3 _smoothPosition;
    private Vector3 _positionVelocity;
    private Quaternion _smoothRotation = Quaternion.identity;
    private bool _smoothInitialized;

    public GhostSpectateService(
        ConfigService configService,
        GhostPlayer ghostPlayer,
        PhotoModeTimelineService photoModeTimelineService,
        PlayerLoopService playerLoopService)
    {
        _configService = configService;
        _ghostPlayer = ghostPlayer;
        _photoModeTimelineService = photoModeTimelineService;
        _playerLoopService = playerLoopService;

        _lateUpdateSubscription = playerLoopService.SubscribeLateUpdate(OnLateUpdate);
        _ghostPlayer.GhostRemoved += OnGhostRemoved;
        PhotoModeApi.PhotoModeExited += OnPhotoModeExited;
    }

    public int? SelectedRecordId { get; private set; }

    public GhostSpectateCameraMode CameraMode { get; private set; } = GhostSpectateCameraMode.ThirdPersonSmooth;

    public bool IsActive =>
        SelectedRecordId.HasValue &&
        _photoModeTimelineService.IsPhotoModeGhostsAvailable;

    public bool ShouldBlockFlyingCamera => IsActive;

    public void SelectGhost(int recordId)
    {
        if (!_ghostPlayer.HasGhost(recordId))
            return;

        SelectedRecordId = recordId;
        ResetSmoothState();
    }

    public void ClearSelection()
    {
        SelectedRecordId = null;
        ResetSmoothState();
    }

    public void SetCameraMode(GhostSpectateCameraMode mode)
    {
        if (CameraMode == mode)
            return;

        CameraMode = mode;
        ResetSmoothState();
    }

    public string GetSelectedDisplayName()
    {
        if (!SelectedRecordId.HasValue ||
            !_ghostPlayer.TryGetGhostData(SelectedRecordId.Value, out GhostData ghostData))
        {
            return null;
        }

        return ghostData.DisplayName;
    }

    private void OnGhostRemoved(object sender, GhostPlayer.GhostRemovedEventArgs e)
    {
        if (SelectedRecordId == e.RecordId)
            ClearSelection();
    }

    private void OnPhotoModeExited()
    {
        ClearSelection();
    }

    private void OnLateUpdate()
    {
        if (!IsActive)
            return;

        if (!TryGetTargetTransform(out Transform targetTransform))
        {
            ClearSelection();
            return;
        }

        if (!PhotoModeFlyingCamera.TryGetCameraTransform(out Transform cameraTransform))
            return;

        ApplyCamera(cameraTransform, targetTransform);
    }

    private void ApplyCamera(Transform cameraTransform, Transform targetTransform)
    {
        ComputeCameraTransform(
            targetTransform,
            out Vector3 targetPosition,
            out Quaternion targetRotation);

        if (CameraMode == GhostSpectateCameraMode.ThirdPersonSmooth)
        {
            if (!_smoothInitialized)
            {
                _smoothPosition = targetPosition;
                _smoothRotation = targetRotation;
                _smoothInitialized = true;
            }

            float smoothTime = Mathf.Max(0.01f, _configService.SpectateThirdPersonSmoothTime.Value);
            _smoothPosition = Vector3.SmoothDamp(
                _smoothPosition,
                targetPosition,
                ref _positionVelocity,
                smoothTime,
                float.PositiveInfinity,
                Time.unscaledDeltaTime);
            _smoothRotation = Quaternion.Slerp(
                _smoothRotation,
                targetRotation,
                Time.unscaledDeltaTime / smoothTime);
            targetPosition = _smoothPosition;
            targetRotation = _smoothRotation;
        }

        cameraTransform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private void ComputeCameraTransform(
        Transform targetTransform,
        out Vector3 position,
        out Quaternion rotation)
    {
        switch (CameraMode)
        {
            case GhostSpectateCameraMode.FirstPerson:
                ComputeFirstPersonTransform(targetTransform, out position, out rotation);
                return;
            default:
                ComputeThirdPersonTransform(targetTransform, out position, out rotation);
                return;
        }
    }

    private void ComputeFirstPersonTransform(
        Transform targetTransform,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = targetTransform.position + Vector3.up * _configService.SpectateFirstPersonEyeHeight.Value;
        rotation = targetTransform.rotation;
    }

    private void ComputeThirdPersonTransform(
        Transform targetTransform,
        out Vector3 position,
        out Quaternion rotation)
    {
        float distance = _configService.SpectateThirdPersonDistance.Value;
        float height = _configService.SpectateThirdPersonHeight.Value;
        float lookHeight = _configService.SpectateThirdPersonLookHeight.Value;

        Vector3 lookTarget = targetTransform.position + Vector3.up * lookHeight;
        position = targetTransform.position
                   - targetTransform.forward * distance
                   + Vector3.up * height;
        rotation = Quaternion.LookRotation(lookTarget - position, Vector3.up);
    }

    private bool TryGetTargetTransform(out Transform targetTransform)
    {
        targetTransform = null;
        if (!SelectedRecordId.HasValue)
            return false;

        if (!_ghostPlayer.TryGetGhostData(SelectedRecordId.Value, out GhostData ghostData) ||
            ghostData.GameObject == null)
        {
            return false;
        }

        targetTransform = ghostData.GameObject.transform;
        return true;
    }

    private void ResetSmoothState()
    {
        _smoothInitialized = false;
        _positionVelocity = Vector3.zero;
    }

    public void Dispose()
    {
        _playerLoopService.UnsubscribeLateUpdate(_lateUpdateSubscription);
        _ghostPlayer.GhostRemoved -= OnGhostRemoved;
        PhotoModeApi.PhotoModeExited -= OnPhotoModeExited;
    }
}
