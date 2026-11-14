using System;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.GraphQL;
using TNRD.Zeepkist.GTR.Patching.Patches;
using ZeepkistClient;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public sealed class LeaderboardPrefetchService : IEagerService, IDisposable
{
    private readonly LeaderboardGraphqlService _graphqlService;
    private readonly ILogger<LeaderboardPrefetchService> _logger;
    private OnlineTabLeaderboardUI _ui;
    private IDisposable _prefetch;
    private string _levelKey;
    private int _pageSize;

    public LeaderboardPrefetchService(LeaderboardGraphqlService graphqlService, ILogger<LeaderboardPrefetchService> logger)
    {
        _graphqlService = graphqlService;
        _logger = logger;
        BaseUI_OverlayVisibility.OverlayOpened += OnOpened;
        BaseUI_OverlayVisibility.OverlayClosed += OnClosed;
        RacingApi.LevelLoaded += OnLevelLoaded;
        RacingApi.PlayerSpawned += Prefetch;
        RacingApi.Quit += Stop;
        MultiplayerApi.DisconnectedFromGame += Stop;
    }

    private void OnOpened(BaseUI ui)
    {
        if (ui is not OnlineTabLeaderboardUI leaderboard) return;
        // Singleplayer can activate GTR before BaseUI.Open completes. Keep that existing subscription.
        if (_ui != null && _ui != leaderboard) Stop();
        _ui = leaderboard;
        Prefetch();
    }

    private void OnClosed(BaseUI ui)
    {
        if (ui == _ui) Stop();
    }

    private void Prefetch()
    {
        if (_ui == null) return;
        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        if (!level.IsAvailable) return;
        int pageSize = ZeepkistNetwork.IsConnectedToGame
            ? _ui.leaderboard_tab_positions.Count
            : OfflineLeaderboardTab.PageSize;
        if (_prefetch != null && _levelKey == level.CacheKey && _pageSize == pageSize) return;
        Release();
        _levelKey = level.CacheKey;
        _pageSize = pageSize;
        _prefetch = _graphqlService.WatchPage(level, 0, pageSize, _ => { },
            error => _logger.LogWarning(error, "GTR leaderboard prefetch subscription failed"));
    }

    private void OnLevelLoaded()
    {
        Release();
        _graphqlService.ClearPages();
        Prefetch();
    }

    private void Release()
    {
        _prefetch?.Dispose();
        _prefetch = null;
        _levelKey = null;
    }

    private void Stop()
    {
        _ui = null;
        Release();
        _graphqlService.ClearPages();
    }

    public void Dispose()
    {
        BaseUI_OverlayVisibility.OverlayOpened -= OnOpened;
        BaseUI_OverlayVisibility.OverlayClosed -= OnClosed;
        RacingApi.LevelLoaded -= OnLevelLoaded;
        RacingApi.PlayerSpawned -= Prefetch;
        RacingApi.Quit -= Stop;
        MultiplayerApi.DisconnectedFromGame -= Stop;
        Stop();
    }
}
