using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public partial class GhostMaterialService : IEagerService
{
    private readonly PlayerLoopService _playerLoopService;
    private readonly GhostPlayer _ghostPlayer;
    private readonly ConfigService _configService;

    private readonly Dictionary<int, GhostData> _ghostData = new();

    public GhostMaterialService(
        PlayerLoopService playerLoopService,
        GhostPlayer ghostPlayer,
        ConfigService configService)
    {
        _playerLoopService = playerLoopService;
        _ghostPlayer = ghostPlayer;
        _configService = configService;

        _playerLoopService.SubscribeUpdate(OnUpdate);
        _ghostPlayer.GhostAdded += OnGhostAdded;
        _ghostPlayer.GhostRemoved += OnGhostRemoved;
    }

    private void OnGhostAdded(object sender, GhostPlayer.GhostAddedEventArgs e)
    {
        _ghostData.Add(e.RecordId, new GhostData(e.Visuals));

        e.Visuals.gameObject.SetActive(_configService.ShowGhosts.Value);

        if (_configService.ShowGhostTransparent.Value)
        {
            _ghostData[e.RecordId].Renderer.Enable();
        }
        else
        {
            _ghostData[e.RecordId].Renderer.Disable();
        }
    }

    private void OnGhostRemoved(object sender, GhostPlayer.GhostRemovedEventArgs e)
    {
        _ghostData.Remove(e.RecordId);
    }

    private void OnUpdate()
    {
        Vector3 cameraPosition = GetCameraPosition();

        foreach ((int recordId, GhostData ghostData) in _ghostData)
        {
            UpdateName(ghostData, cameraPosition);
        }

        HandleToggleTransparency();
    }

    private static Vector3 GetCameraPosition()
    {
        GameMaster master = PlayerManager.Instance.currentMaster;
        if (master == null)
            return Vector3.zero;

        if (master.isPhotoMode)
        {
            return master.flyingCamera.GetCameraPosition();
        }

        if (master.carSetups.Count > 0)
        {
            return master.carSetups[0].theCamera.transform.position;
        }

        return Vector3.zero;
    }

    private static void UpdateName(GhostData ghostData, Vector3 cameraPosition)
    {
        Transform nameDisplayTransform = ghostData.Visuals.NameDisplay.transform;
        nameDisplayTransform.position = ghostData.Visuals.transform.position + Vector3.up * 2.5f;
        nameDisplayTransform.LookAt(cameraPosition);
        nameDisplayTransform.LookAt(nameDisplayTransform.position - nameDisplayTransform.forward);
    }

    private void HandleToggleTransparency()
    {
        if (!Input.GetKeyDown(_configService.ToggleShowGhostTransparent.Value))
            return;

        _configService.ShowGhostTransparent.Value = !_configService.ShowGhostTransparent.Value;

        foreach ((int _, GhostData ghostData) in _ghostData)
        {
            if (_configService.ShowGhostTransparent.Value)
            {
                ghostData.Renderer.Enable();
            }
            else
            {
                ghostData.Renderer.Disable();
            }
        }
    }
}
