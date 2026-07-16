using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TNRD.Zeepkist.GTR.Core;
using UnityEngine;
using ZeepSDK.PhotoMode;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GtrSpectateTargetService : IEagerService, IDisposable
{
    private static readonly MethodInfo ResetHelperMethod =
        AccessTools.Method(typeof(FlyingCameraScript), "ResetHelper");

    private readonly GhostPlayer _ghostPlayer;
    private readonly PhotoModeTimelineService _photoModeTimelineService;

    private readonly Dictionary<Transform, int> _transformToRecordId = new();
    private int? _currentRecordId;

    public GtrSpectateTargetService(
        GhostPlayer ghostPlayer,
        PhotoModeTimelineService photoModeTimelineService)
    {
        _ghostPlayer = ghostPlayer;
        _photoModeTimelineService = photoModeTimelineService;

        _ghostPlayer.GhostAdded += OnGhostsChanged;
        _ghostPlayer.GhostRemoved += OnGhostsChanged;
        PhotoModeApi.PhotoModeEntered += OnPhotoModeEntered;
        PhotoModeApi.PhotoModeExited += OnPhotoModeExited;
    }

    public bool ShouldInjectGhosts => _photoModeTimelineService.IsPhotoModeGhostsAvailable;

    public bool TryGetRecordId(Transform transform, out int recordId)
    {
        return _transformToRecordId.TryGetValue(transform, out recordId);
    }

    public void ApplyGhostTargets(FlyingCameraScript camera)
    {
        if (camera == null)
            return;

        _transformToRecordId.Clear();
        camera.targetList.Clear();

        foreach (LoadedGhostEntry ghost in _ghostPlayer.GetLoadedGhosts())
        {
            Transform transform = GetSpectateTransform(ghost.GhostData);
            if (transform == null)
                continue;

            _transformToRecordId[transform] = ghost.RecordId;
            camera.targetList.Add(new SpectatorZeepkistTarget
            {
                transform = transform,
                name = ghost.GetListLabel(),
                ghost = null
            });
        }

        ReconcileCurrentTarget(camera);
        ResetHelperMethod?.Invoke(camera, null);
    }

    public int GetCurrentTargetIndex(FlyingCameraScript camera)
    {
        if (camera.targetList.Count == 0)
            return 0;

        if (_currentRecordId.HasValue)
        {
            for (var i = 0; i < camera.targetList.Count; i++)
            {
                Transform transform = camera.targetList[i].transform;
                if (transform != null &&
                    _transformToRecordId.TryGetValue(transform, out int recordId) &&
                    recordId == _currentRecordId.Value)
                {
                    return i;
                }
            }
        }

        Transform currentTransform = camera.currentTarget?.transform;
        if (currentTransform == null)
            return 0;

        for (var i = 0; i < camera.targetList.Count; i++)
        {
            if (currentTransform != camera.targetList[i].transform)
                continue;

            SyncCurrentRecordId(camera);
            return i;
        }

        return 0;
    }

    public void SyncCurrentRecordId(FlyingCameraScript camera)
    {
        Transform currentTransform = camera.currentTarget?.transform;
        if (currentTransform == null ||
            !_transformToRecordId.TryGetValue(currentTransform, out int recordId))
        {
            _currentRecordId = null;
            return;
        }

        _currentRecordId = recordId;
    }

    private void ReconcileCurrentTarget(FlyingCameraScript camera)
    {
        if (TrySelectTargetByRecordId(camera, _currentRecordId))
            return;

        Transform currentTransform = camera.currentTarget?.transform;
        if (currentTransform != null)
        {
            for (var i = 0; i < camera.targetList.Count; i++)
            {
                if (currentTransform != camera.targetList[i].transform)
                    continue;

                camera.currentTarget = camera.targetList[i];
                SyncCurrentRecordId(camera);
                return;
            }
        }

        camera.currentTarget = camera.targetList.Count != 0
            ? camera.targetList[0]
            : new SpectatorZeepkistTarget();
        SyncCurrentRecordId(camera);
    }

    private bool TrySelectTargetByRecordId(FlyingCameraScript camera, int? recordId)
    {
        if (!recordId.HasValue)
            return false;

        for (var i = 0; i < camera.targetList.Count; i++)
        {
            Transform transform = camera.targetList[i].transform;
            if (transform == null ||
                !_transformToRecordId.TryGetValue(transform, out int targetRecordId) ||
                targetRecordId != recordId.Value)
            {
                continue;
            }

            camera.currentTarget = camera.targetList[i];
            _currentRecordId = recordId;
            return true;
        }

        return false;
    }

    private static Transform GetSpectateTransform(GhostData ghostData)
    {
        if (ghostData == null)
            return null;

        return ghostData.Visuals?.GhostModel != null
            ? ghostData.Visuals.GhostModel.transform
            : ghostData.GameObject?.transform;
    }

    private void OnPhotoModeEntered()
    {
        RefreshFlyingCameraTargets();
    }

    private void OnPhotoModeExited()
    {
        _transformToRecordId.Clear();
        _currentRecordId = null;
    }

    private void OnGhostsChanged(object sender, EventArgs e)
    {
        RefreshFlyingCameraTargets();
    }

    private void RefreshFlyingCameraTargets()
    {
        if (!ShouldInjectGhosts || PhotoModeFlyingCamera.Current == null)
            return;

        PhotoModeFlyingCamera.Current.SetCurrentZeepkist();
    }

    public void Dispose()
    {
        _ghostPlayer.GhostAdded -= OnGhostsChanged;
        _ghostPlayer.GhostRemoved -= OnGhostsChanged;
        PhotoModeApi.PhotoModeEntered -= OnPhotoModeEntered;
        PhotoModeApi.PhotoModeExited -= OnPhotoModeExited;
    }
}
