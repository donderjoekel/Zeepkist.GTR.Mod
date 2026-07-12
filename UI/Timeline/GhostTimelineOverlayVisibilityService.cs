using System;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GhostTimelineOverlayVisibilityService : IEagerService, IDisposable
{
    private readonly GhostTimelineState _timelineState;
    private readonly PlayerLoopService _playerLoopService;
    private readonly PlayerLoopSubscription _updateSubscription;

    private PauseMenuUI _pauseMenu;
    private SettingsUI _settingsUi;
    private OnlineTabLeaderboardUI _gameplayLeaderboard;
    private OnlineTabLeaderboardUI[] _leaderboardInstances;

    public GhostTimelineOverlayVisibilityService(
        GhostTimelineState timelineState,
        PlayerLoopService playerLoopService)
    {
        _timelineState = timelineState;
        _playerLoopService = playerLoopService;
        _updateSubscription = _playerLoopService.SubscribeUpdate(OnUpdate);

        RacingApi.PlayerSpawned += RefreshUiReferences;
        RefreshUiReferences();
    }

    private void OnUpdate()
    {
        EnsureUiReferences();

        var hide = IsOpen(_pauseMenu)
            || IsOpen(_settingsUi)
            || IsAnyLeaderboardOpen();

        _timelineState.SetHiddenByOverlay(hide);
    }

    private void EnsureUiReferences()
    {
        if (_pauseMenu == null)
            _pauseMenu = UnityEngine.Object.FindObjectOfType<PauseMenuUI>(true);

        if (_settingsUi == null)
            _settingsUi = UnityEngine.Object.FindObjectOfType<SettingsUI>(true);

        if (_gameplayLeaderboard == null)
            _gameplayLeaderboard = PlayerManager.Instance?.currentMaster?.OnlineGameplayUI?.OnlineTabLeaderboard;
    }

    private void RefreshUiReferences()
    {
        _pauseMenu = null;
        _settingsUi = null;
        _gameplayLeaderboard = null;
        _leaderboardInstances = null;
        EnsureUiReferences();
    }

    private bool IsAnyLeaderboardOpen()
    {
        if (IsOpen(_gameplayLeaderboard))
            return true;

        _leaderboardInstances ??= UnityEngine.Object.FindObjectsOfType<OnlineTabLeaderboardUI>(true);
        foreach (var leaderboard in _leaderboardInstances)
        {
            if (IsOpen(leaderboard))
                return true;
        }

        return false;
    }

    private static bool IsOpen(BaseUI ui) => ui != null && ui.IsOpen;

    public void Dispose()
    {
        RacingApi.PlayerSpawned -= RefreshUiReferences;
        _playerLoopService.UnsubscribeUpdate(_updateSubscription);
    }
}
