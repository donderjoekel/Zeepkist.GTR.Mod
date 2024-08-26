using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostMaterialService : IEagerService
{
    private readonly PlayerLoopService _playerLoopService;
    private readonly GhostPlayer _ghostPlayer;
    private readonly ConfigService _configService;
    private readonly MessengerService _messengerService;

    public GhostMaterialService(
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
        e.GhostData.GameObject.SetActive(_configService.ShowGhosts.Value);

        if (_configService.ShowGhostTransparent.Value)
        {
            e.GhostData.Renderer.SwitchToGhost();
        }
        else
        {
            e.GhostData.Renderer.SwitchToNormal();
        }
    }

    private void OnUpdate()
    {
        HandleToggleTransparency();
        UpdateRenderers();
    }

    private void HandleToggleTransparency()
    {
        if (!Input.GetKeyDown(_configService.ToggleShowGhostTransparent.Value))
            return;

        _configService.ShowGhostTransparent.Value = !_configService.ShowGhostTransparent.Value;

        foreach (GhostData ghostData in _ghostPlayer.ActiveGhosts)
        {
            if (_configService.ShowGhostTransparent.Value)
            {
                ghostData.Renderer.SwitchToGhost();
            }
            else
            {
                ghostData.Renderer.SwitchToNormal();
            }
        }

        if (_configService.ShowGhostTransparent.Value)
        {
            _messengerService.Log("Transparent Ghosts");
        }
        else
        {
            _messengerService.Log("Opaque Ghosts");
        }
    }

    private void UpdateRenderers()
    {
        foreach (GhostData ghostData in _ghostPlayer.ActiveGhosts)
        {
            UpdateRenderer(ghostData);
        }
    }

    private void UpdateRenderer(GhostData ghostData)
    {
        const float minDistance = 2.5f;
        const float maxDistance = 8f;
        float maxAlpha = _configService.ShowGhostTransparent.Value ? 0.3f : 1f;

        if (PlayerManager.Instance == null || PlayerManager.Instance.currentMaster == null)
            return;

        float playerDistance = 1000;

        if (!PlayerManager.Instance.currentMaster.isPhotoMode)
        {
            if (PlayerManager.Instance.currentMaster.carSetups != null &&
                PlayerManager.Instance.currentMaster.carSetups.Count > 0)
            {
                playerDistance = Vector3.Distance(
                    ghostData.GameObject.transform.position,
                    PlayerManager.Instance.currentMaster.carSetups[0].transform.position);
            }
        }

        float inverseLerp = Mathf.InverseLerp(minDistance, maxDistance, playerDistance);
        float fadeAmount = Mathf.Lerp(0, maxAlpha, inverseLerp);

        ghostData.Visuals.NameDisplay.theDisplayName.color = ghostData.Visuals.NameDisplay.theDisplayName.color with
        {
            a = inverseLerp
        };

        if (_configService.ShowGhostTransparent.Value)
        {
            Color color = ghostData.Ghost.Color with
            {
                a = fadeAmount
            };

            ghostData.Renderer.SetGhostColor(color);
        }
        else
        {
            ghostData.Renderer.SetFade(fadeAmount);
        }
    }
}
