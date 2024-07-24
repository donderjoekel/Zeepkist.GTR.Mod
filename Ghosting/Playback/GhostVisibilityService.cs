using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostVisibilityService : IEagerService
{
    private readonly PlayerLoopService _playerLoopService;
    private readonly GhostPlayer _ghostPlayer;
    private readonly ConfigService _configService;
    private readonly MessengerService _messengerService;

    public GhostVisibilityService(
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

        if (_configService.ShowGhosts.Value)
        {
            e.GhostData.Renderer.Enable();
        }
        else
        {
            e.GhostData.Renderer.Disable();
        }
    }

    private void OnUpdate()
    {
        HandleToggleTransparency();
    }

    private void HandleToggleTransparency()
    {
        if (!Input.GetKeyDown(_configService.ToggleShowGhosts.Value))
            return;

        _configService.ShowGhosts.Value = !_configService.ShowGhosts.Value;

        foreach (GhostData ghostData in _ghostPlayer.ActiveGhosts)
        {
            if (_configService.ShowGhosts.Value)
            {
                ghostData.Renderer.Enable();
            }
            else
            {
                ghostData.Renderer.Disable();
            }
        }

        if (_configService.ShowGhosts.Value)
        {
            _messengerService.Log("Showing Ghosts");
        }
        else
        {
            _messengerService.Log("Hiding Ghosts");
        }
    }
}
