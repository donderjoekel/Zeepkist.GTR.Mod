using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostNamePositioningService : IEagerService
{
    private readonly PlayerLoopService _playerLoopService;
    private readonly GhostPlayer _ghostPlayer;
    private readonly ConfigService _configService;
    private readonly MessengerService _messengerService;
    private readonly BulkGhostModeState _bulkModeState;

    public GhostNamePositioningService(
        PlayerLoopService playerLoopService,
        GhostPlayer ghostPlayer,
        ConfigService configService,
        MessengerService messengerService,
        BulkGhostModeState bulkModeState)
    {
        _playerLoopService = playerLoopService;
        _ghostPlayer = ghostPlayer;
        _configService = configService;
        _messengerService = messengerService;
        _bulkModeState = bulkModeState;

        _playerLoopService.SubscribeUpdate(OnUpdate);
        _ghostPlayer.GhostAdded += OnGhostAdded;
        _bulkModeState.Changed += OnBulkModeChanged;
    }

    private void OnGhostAdded(object sender, GhostPlayer.GhostAddedEventArgs e)
    {
        UpdateVisibility(e.GhostData);
    }

    private void UpdateVisibility(GhostData ghostData)
    {
        if (ghostData.VisualProfile == GhostVisualProfile.Bulk)
            return;

        bool visible = ghostData.PlaybackVisible &&
                       _configService.ShowGhostNames.Value &&
                       _configService.ShowGhosts.Value;
        if (ghostData.Visuals.NameDisplay.gameObject.activeSelf != visible)
            ghostData.Visuals.NameDisplay.gameObject.SetActive(visible);

        if (_bulkModeState.IsActive)
            SetNameAlpha(ghostData, 1);
    }

    private void OnUpdate()
    {
        Vector3 cameraPosition = GetCameraPosition();
        foreach (GhostData ghostData in _ghostPlayer.ActiveGhosts)
        {
            if (ghostData.VisualProfile == GhostVisualProfile.Bulk)
                continue;

            UpdateVisibility(ghostData);
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
        if (!ghostData.PlaybackVisible)
            return;

        Transform nameDisplayTransform = ghostData.Visuals.NameDisplay.transform;
        Transform anchor = ghostData.NameAnchor != null ? ghostData.NameAnchor : ghostData.GameObject.transform;
        nameDisplayTransform.position = anchor.position + Vector3.up * 2.5f;
        nameDisplayTransform.LookAt(cameraPosition);
        nameDisplayTransform.LookAt(nameDisplayTransform.position - nameDisplayTransform.forward);
    }

    private void OnBulkModeChanged()
    {
        foreach (GhostData ghostData in _ghostPlayer.ActiveGhosts)
        {
            UpdateVisibility(ghostData);
        }
    }

    private static void SetNameAlpha(GhostData ghostData, float alpha)
    {
        ghostData.Visuals.NameDisplay.theDisplayName.color =
            ghostData.Visuals.NameDisplay.theDisplayName.color with
            {
                a = alpha
            };
    }
}
