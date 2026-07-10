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
            Transform transform = ghost.GhostData.GameObject?.transform;
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

    private static void ReconcileCurrentTarget(FlyingCameraScript camera)
    {
        Transform currentTransform = camera.currentTarget?.transform;
        var currentTargetExists = false;

        if (currentTransform != null)
        {
            for (var i = 0; i < camera.targetList.Count; i++)
            {
                if (currentTransform == camera.targetList[i].transform)
                {
                    currentTargetExists = true;
                    break;
                }
            }
        }

        if (!currentTargetExists)
        {
            camera.currentTarget = camera.targetList.Count != 0
                ? camera.targetList[0]
                : new SpectatorZeepkistTarget();
        }
    }

    private void OnPhotoModeEntered()
    {
        RefreshFlyingCameraTargets();
    }

    private void OnPhotoModeExited()
    {
        _transformToRecordId.Clear();
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
