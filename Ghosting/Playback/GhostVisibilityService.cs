using System;
using System.Linq;
using BepInEx.Configuration;
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

        _configService.ShowGhosts.SettingChanged += OnShowGhostsChanged;
        _configService.ShowGlobalPersonalBest.SettingChanged += OnShowGlobalChanged;
    }

    private void OnShowGhostsChanged(object sender, EventArgs e)
    {
        foreach (GhostData ghostData in _ghostPlayer.ActiveGhosts)
        {
            UpdateGhostsVisibility(ghostData);
        }
    }

    private void OnShowGlobalChanged(object sender, EventArgs e)
    {
        UpdateGhostsVisibility(_ghostPlayer.ActiveGhosts.FirstOrDefault(x => x.Type == GhostType.Global));
    }

    private void OnGhostAdded(object sender, GhostPlayer.GhostAddedEventArgs e)
    {
        UpdateGhostsVisibility(e.GhostData);
    }

    private void UpdateGhostsVisibility(GhostData ghostData)
    {
        if (ghostData == null)
        {
            return;
        }

        ghostData.Visuals.gameObject.SetActive(_configService.ShowGhosts.Value);

        if (!_configService.ShowGhosts.Value)
        {
            return;
        }

        ConfigEntry<bool> configEntry = ghostData.Type switch
        {
            GhostType.Global => _configService.ShowGlobalPersonalBest,
            _ => throw new ArgumentOutOfRangeException()
        };

        ghostData.Visuals.gameObject.SetActive(configEntry.Value);
    }

    private void OnUpdate()
    {
        HandleToggleTransparency();
    }

    private void HandleToggleTransparency()
    {
        HandleShowGhosts();
        HandleShowGlobal();
    }

    private void HandleShowGhosts()
    {
        if (!Input.GetKeyDown(_configService.ToggleShowGhosts.Value))
        {
            return;
        }

        _configService.ShowGhosts.Value = !_configService.ShowGhosts.Value;
        _messengerService.Log(_configService.ShowGhosts.Value ? "Showing Ghosts" : "Hiding Ghosts");
    }

    private void HandleShowing(ConfigEntry<KeyCode> keyEntry, ConfigEntry<bool> toggleEntry, GhostType ghostType)
    {
        if (!Input.GetKeyDown(keyEntry.Value))
        {
            return;
        }

        toggleEntry.Value = !toggleEntry.Value;
        _messengerService.Log(toggleEntry.Value
            ? $"Showing {ghostType} Personal Best"
            : $"Hiding {ghostType} Personal Best");
    }

    private void HandleShowGlobal()
    {
        HandleShowing(_configService.ToggleShowGlobalPersonalBest,
            _configService.ShowGlobalPersonalBest,
            GhostType.Global);
    }
}
