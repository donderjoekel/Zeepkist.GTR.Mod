using System;
using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Core;
using UnityEngine;
using ZeepSDK.PhotoMode;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GtrGhostSpectateRigService : IEagerService, IDisposable
{
    private readonly GhostPlayer _ghostPlayer;

    private readonly Dictionary<int, GtrGhostSpectateRig> _rigsByRecordId = new();
    private readonly Dictionary<Transform, GtrGhostSpectateRig> _rigsByTransform = new();

    public GtrGhostSpectateRigService(GhostPlayer ghostPlayer)
    {
        _ghostPlayer = ghostPlayer;

        _ghostPlayer.GhostAdded += OnGhostAdded;
        _ghostPlayer.GhostRemoved += OnGhostRemoved;
        PhotoModeApi.PhotoModeEntered += OnPhotoModeEntered;
        PhotoModeApi.PhotoModeExited += OnPhotoModeExited;
    }

    public bool TryGetRig(Transform soapboxTransform, out GtrGhostSpectateRig rig)
    {
        if (soapboxTransform == null)
        {
            rig = null;
            return false;
        }

        return _rigsByTransform.TryGetValue(soapboxTransform, out rig);
    }

    public bool HasRigForRecord(int recordId)
    {
        return _rigsByRecordId.ContainsKey(recordId);
    }

    private void OnGhostAdded(object sender, GhostPlayer.GhostAddedEventArgs e)
    {
        TryCreateRig(e.RecordId, e.GhostData);
    }

    private void OnGhostRemoved(object sender, GhostPlayer.GhostRemovedEventArgs e)
    {
        RemoveRig(e.RecordId);
    }

    private void OnPhotoModeEntered()
    {
        foreach (LoadedGhostEntry ghost in _ghostPlayer.GetLoadedGhosts())
            TryCreateRig(ghost.RecordId, ghost.GhostData);
    }

    private void OnPhotoModeExited()
    {
        ClearAllRigs();
    }

    private void TryCreateRig(int recordId, GhostData ghostData)
    {
        if (ghostData == null || _rigsByRecordId.ContainsKey(recordId))
            return;

        GtrGhostSpectateRig rig = GtrGhostSpectateRig.TryCreate(ghostData);
        if (rig == null)
            return;

        _rigsByRecordId[recordId] = rig;
        _rigsByTransform[rig.SoapboxRoot.transform] = rig;
    }

    private void RemoveRig(int recordId)
    {
        if (!_rigsByRecordId.TryGetValue(recordId, out GtrGhostSpectateRig rig))
            return;

        if (rig.SoapboxRoot != null)
            _rigsByTransform.Remove(rig.SoapboxRoot.transform);

        _rigsByRecordId.Remove(recordId);

        if (rig != null)
            Object.Destroy(rig.gameObject);
    }

    private void ClearAllRigs()
    {
        foreach (GtrGhostSpectateRig rig in _rigsByRecordId.Values)
        {
            if (rig != null)
                Object.Destroy(rig.gameObject);
        }

        _rigsByRecordId.Clear();
        _rigsByTransform.Clear();
    }

    public void Dispose()
    {
        _ghostPlayer.GhostAdded -= OnGhostAdded;
        _ghostPlayer.GhostRemoved -= OnGhostRemoved;
        PhotoModeApi.PhotoModeEntered -= OnPhotoModeEntered;
        PhotoModeApi.PhotoModeExited -= OnPhotoModeExited;
        ClearAllRigs();
    }
}
