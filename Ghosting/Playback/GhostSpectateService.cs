using System;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;
using TNRD.Zeepkist.GTR.UI;
using UnityEngine;
using ZeepSDK.PhotoMode;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostSpectateService : IEagerService, IDisposable
{
    private readonly ConfigService _configService;
    private readonly GhostPlayer _ghostPlayer;
    private readonly PhotoModeTimelineService _photoModeTimelineService;
    private readonly PlayerLoopService _playerLoopService;
    private readonly PlaybackUiInputState _playbackUiInputState;
    private readonly PlayerLoopSubscription _lateUpdateSubscription;

    private Vector3 _smoothPosition;
    private Vector3 _positionVelocity;
    private Quaternion _smoothRotation = Quaternion.identity;
    private bool _smoothInitialized;
    private float _firstPersonHeightOffset;
    private float _thirdPersonDistanceOffset;
    private float _topDownHeightOffset;

    private const float FirstPersonHeightScrollStep = 0.1f;
    private const float ThirdPersonDistanceScrollStep = 0.75f;
    private const float TopDownHeightScrollStep = 2f;

    private const float MinFirstPersonEyeHeight = 0.5f;
    private const float MaxFirstPersonEyeHeight = 5f;
    private const float MinThirdPersonDistance = 1f;
    private const float MaxThirdPersonDistance = 50f;
    private const float MinTopDownHeight = 5f;
    private const float MaxTopDownHeight = 100f;

    public GhostSpectateService(
        ConfigService configService,
        GhostPlayer ghostPlayer,
        PhotoModeTimelineService photoModeTimelineService,
        PlayerLoopService playerLoopService,
        PlaybackUiInputState playbackUiInputState)
    {
        _configService = configService;
        _ghostPlayer = ghostPlayer;
        _photoModeTimelineService = photoModeTimelineService;
        _playerLoopService = playerLoopService;
        _playbackUiInputState = playbackUiInputState;

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
        ResetSessionOffsets();
        ResetSmoothState();
    }

    public void ClearSelection()
    {
        SelectedRecordId = null;
        ResetSessionOffsets();
        ResetSmoothState();
    }

    public void SetCameraMode(GhostSpectateCameraMode mode)
    {
        if (CameraMode == mode)
            return;

        CameraMode = mode;
        ResetSessionOffsets();
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

        HandleScrollInput();

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
            case GhostSpectateCameraMode.TopDown:
                ComputeTopDownTransform(targetTransform, out position, out rotation);
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
        position = targetTransform.position + Vector3.up * GetFirstPersonEyeHeight();
        rotation = targetTransform.rotation;
    }

    private float GetFirstPersonEyeHeight()
    {
        return Mathf.Clamp(
            _configService.SpectateFirstPersonEyeHeight.Value + _firstPersonHeightOffset,
            MinFirstPersonEyeHeight,
            MaxFirstPersonEyeHeight);
    }

    private float GetThirdPersonDistance()
    {
        return Mathf.Clamp(
            _configService.SpectateThirdPersonDistance.Value + _thirdPersonDistanceOffset,
            MinThirdPersonDistance,
            MaxThirdPersonDistance);
    }

    private float GetTopDownHeight()
    {
        return Mathf.Clamp(
            _configService.SpectateTopDownHeight.Value + _topDownHeightOffset,
            MinTopDownHeight,
            MaxTopDownHeight);
    }

    private void ComputeTopDownTransform(
        Transform targetTransform,
        out Vector3 position,
        out Quaternion rotation)
    {
        float height = GetTopDownHeight();
        float pitchDegrees = Mathf.Clamp(_configService.SpectateTopDownPitch.Value, 0f, 80f);
        float lookHeight = _configService.SpectateThirdPersonLookHeight.Value;
        Vector3 lookTarget = targetTransform.position + Vector3.up * lookHeight;

        if (pitchDegrees <= 0.01f)
        {
            position = targetTransform.position + Vector3.up * height;
            rotation = Quaternion.LookRotation(lookTarget - position, targetTransform.forward);
            return;
        }

        float pitchRad = pitchDegrees * Mathf.Deg2Rad;
        float vertical = Mathf.Cos(pitchRad) * height;
        float horizontal = Mathf.Sin(pitchRad) * height;
        position = targetTransform.position
                   + Vector3.up * vertical
                   - targetTransform.forward * horizontal;
        rotation = Quaternion.LookRotation(lookTarget - position, Vector3.up);
    }

    private void ComputeThirdPersonTransform(
        Transform targetTransform,
        out Vector3 position,
        out Quaternion rotation)
    {
        float distance = GetThirdPersonDistance();
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

    private void HandleScrollInput()
    {
        if (_playbackUiInputState.IsPointerOverGtrWindow)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f)
            return;

        var changed = false;
        switch (CameraMode)
        {
            case GhostSpectateCameraMode.FirstPerson:
                changed = AdjustScrollOffset(
                    ref _firstPersonHeightOffset,
                    _configService.SpectateFirstPersonEyeHeight.Value,
                    scroll * FirstPersonHeightScrollStep,
                    MinFirstPersonEyeHeight,
                    MaxFirstPersonEyeHeight);
                break;
            case GhostSpectateCameraMode.ThirdPersonStrict:
            case GhostSpectateCameraMode.ThirdPersonSmooth:
                changed = AdjustScrollOffset(
                    ref _thirdPersonDistanceOffset,
                    _configService.SpectateThirdPersonDistance.Value,
                    -scroll * ThirdPersonDistanceScrollStep,
                    MinThirdPersonDistance,
                    MaxThirdPersonDistance);
                break;
            case GhostSpectateCameraMode.TopDown:
                changed = AdjustScrollOffset(
                    ref _topDownHeightOffset,
                    _configService.SpectateTopDownHeight.Value,
                    -scroll * TopDownHeightScrollStep,
                    MinTopDownHeight,
                    MaxTopDownHeight);
                break;
        }

        if (changed)
            ResetSmoothState();
    }

    private static bool AdjustScrollOffset(
        ref float offset,
        float configValue,
        float delta,
        float min,
        float max)
    {
        float effective = Mathf.Clamp(configValue + offset + delta, min, max);
        float newOffset = effective - configValue;
        if (Mathf.Approximately(newOffset, offset))
            return false;

        offset = newOffset;
        return true;
    }

    private void ResetSessionOffsets()
    {
        _firstPersonHeightOffset = 0f;
        _thirdPersonDistanceOffset = 0f;
        _topDownHeightOffset = 0f;
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
