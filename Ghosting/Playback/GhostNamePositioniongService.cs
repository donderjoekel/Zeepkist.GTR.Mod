using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostNamePositioniongService : IEagerService
{
    private readonly PlayerLoopService _playerLoopService;
    private readonly GhostPlayer _ghostPlayer;
    private readonly ConfigService _configService;
    private readonly MessengerService _messengerService;

    public GhostNamePositioniongService(
        PlayerLoopService playerLoopService,
        GhostPlayer ghostPlayer,
        ConfigService configService,
        MessengerService messengerService)
    {
        _playerLoopService = playerLoopService;
        _ghostPlayer = ghostPlayer;
        _configService = configService;
        _messengerService = messengerService;

        _playerLoopService.SubscribeUpdate(OnUpdate);
        _ghostPlayer.GhostAdded += OnGhostAdded;
    }

    private void OnGhostAdded(object sender, GhostPlayer.GhostAddedEventArgs e)
    {
        UpdateVisibility(e.GhostData);
    }

    private void UpdateVisibility(GhostData ghostData)
    {
        ghostData.Visuals.NameDisplay.gameObject.SetActive(
            _configService.ShowGhostNames.Value && _configService.ShowGhosts.Value);
    }

    private void OnUpdate()
    {
        Vector3 cameraPosition = GetCameraPosition();
        foreach (GhostData ghostData in _ghostPlayer.ActiveGhosts)
        {
            UpdateName(ghostData, cameraPosition);
        }

        HandleToggleNameDisplay();
    }

    private void HandleToggleNameDisplay()
    {
        if (!Input.GetKeyDown(_configService.ToggleShowGhostNames.Value))
            return;

        _configService.ShowGhostNames.Value = !_configService.ShowGhostNames.Value;

        foreach (GhostData ghostData in _ghostPlayer.ActiveGhosts)
        {
            UpdateVisibility(ghostData);
        }

        if (_configService.ShowGhostNames.Value)
        {
            _messengerService.Log("Showing Ghost Names");
        }
        else
        {
            _messengerService.Log("Hiding Ghost Names");
        }
    }

    private static Vector3 GetCameraPosition()
    {
        if (PlayerManager.Instance == null)
            return Vector3.zero;

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
        nameDisplayTransform.position = ghostData.GameObject.transform.position + Vector3.up * 2.5f;
        nameDisplayTransform.LookAt(cameraPosition);
        nameDisplayTransform.LookAt(nameDisplayTransform.position - nameDisplayTransform.forward);
    }
}
