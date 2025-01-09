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
        _configService.ShowYearlyPersonalBest.SettingChanged += OnShowYearlyChanged;
        _configService.ShowQuarterlyPersonalBest.SettingChanged += OnShowQuarterlyChanged;
        _configService.ShowMonthlyPersonalBest.SettingChanged += OnShowMonthlyChanged;
        _configService.ShowWeeklyPersonalBest.SettingChanged += OnShowWeeklyChanged;
        _configService.ShowDailyPersonalBest.SettingChanged += OnShowDailyChanged;
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

    private void OnShowYearlyChanged(object sender, EventArgs e)
    {
        UpdateGhostsVisibility(_ghostPlayer.ActiveGhosts.FirstOrDefault(x => x.Type == GhostType.Yearly));
    }

    private void OnShowQuarterlyChanged(object sender, EventArgs e)
    {
        UpdateGhostsVisibility(_ghostPlayer.ActiveGhosts.FirstOrDefault(x => x.Type == GhostType.Quarterly));
    }

    private void OnShowMonthlyChanged(object sender, EventArgs e)
    {
        UpdateGhostsVisibility(_ghostPlayer.ActiveGhosts.FirstOrDefault(x => x.Type == GhostType.Monthly));
    }

    private void OnShowWeeklyChanged(object sender, EventArgs e)
    {
        UpdateGhostsVisibility(_ghostPlayer.ActiveGhosts.FirstOrDefault(x => x.Type == GhostType.Weekly));
    }

    private void OnShowDailyChanged(object sender, EventArgs e)
    {
        UpdateGhostsVisibility(_ghostPlayer.ActiveGhosts.FirstOrDefault(x => x.Type == GhostType.Daily));
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
            GhostType.Daily => _configService.ShowDailyPersonalBest,
            GhostType.Weekly => _configService.ShowWeeklyPersonalBest,
            GhostType.Monthly => _configService.ShowMonthlyPersonalBest,
            GhostType.Quarterly => _configService.ShowQuarterlyPersonalBest,
            GhostType.Yearly => _configService.ShowYearlyPersonalBest,
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
        HandleShowYearly();
        HandleShowQuarterly();
        HandleShowMonthly();
        HandleShowWeekly();
        HandleShowDaily();
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

    private void HandleShowYearly()
    {
        HandleShowing(_configService.ToggleShowYearlyPersonalBest,
            _configService.ShowYearlyPersonalBest,
            GhostType.Yearly);
    }

    private void HandleShowQuarterly()
    {
        HandleShowing(_configService.ToggleShowQuarterlyPersonalBest,
            _configService.ShowQuarterlyPersonalBest,
            GhostType.Quarterly);
    }

    private void HandleShowMonthly()
    {
        HandleShowing(_configService.ToggleShowMonthlyPersonalBest,
            _configService.ShowMonthlyPersonalBest,
            GhostType.Monthly);
    }

    private void HandleShowWeekly()
    {
        HandleShowing(_configService.ToggleShowWeeklyPersonalBest,
            _configService.ShowWeeklyPersonalBest,
            GhostType.Weekly);
    }

    private void HandleShowDaily()
    {
        HandleShowing(_configService.ToggleShowDailyPersonalBest,
            _configService.ShowDailyPersonalBest,
            GhostType.Daily);
    }
}
