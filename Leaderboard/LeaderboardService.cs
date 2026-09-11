using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.GraphQL;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Leaderboard;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class LeaderboardService : IEagerService, IDisposable
{
    private const int FirstTournamentTabIndex = 3;

    private readonly TrackTournamentGraphqlService _trackTournamentGraphqlService;
    private readonly ILogger<LeaderboardService> _logger;
    private readonly List<TrackTournamentLeaderboardTabBase> _tournamentTabs = [];

    private CancellationTokenSource _lookupCancellationTokenSource;
    private int _generation;
    private string _levelKey;

    public LeaderboardService(
        OnlineLeaderboardTab onlineLeaderboardTab,
        OfflineLeaderboardTab offlineLeaderboardTab,
        TrackTournamentGraphqlService trackTournamentGraphqlService,
        ILogger<LeaderboardService> logger)
    {
        _trackTournamentGraphqlService = trackTournamentGraphqlService;
        _logger = logger;

        try
        {
            LeaderboardApi.AddTab(offlineLeaderboardTab);
            LeaderboardApi.AddTab(onlineLeaderboardTab);
            RacingApi.LevelLoaded += RefreshTournamentTabs;
            RacingApi.PlayerSpawned += EnsureTournamentTabs;
            RacingApi.Quit += ClearTournamentTabs;
            EnsureTournamentTabs();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to register leaderboard tabs");
            throw;
        }
    }

    private void RefreshTournamentTabs()
    {
        ClearTournamentTabs();
        EnsureTournamentTabs();
    }

    private void EnsureTournamentTabs()
    {
        if (_levelKey != null)
            return;

        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        if (!level.IsAvailable)
            return;

        _levelKey = level.CacheKey;
        int generation = ++_generation;
        _lookupCancellationTokenSource = new CancellationTokenSource();
        LoadTournamentTabsAsync(level, generation, _lookupCancellationTokenSource.Token).Forget();
    }

    private async UniTaskVoid LoadTournamentTabsAsync(
        LevelGraphqlIdentity level,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<TrackTournamentDescriptor> tournaments =
                await _trackTournamentGraphqlService.GetForLevelAsync(
                    level,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
            await UniTask.SwitchToMainThread(cancellationToken);
            if (generation != _generation)
                return;

            for (int i = 0; i < tournaments.Count; i++)
            {
                OnlineTrackTournamentLeaderboardTab onlineTab =
                    new(_trackTournamentGraphqlService, tournaments[i]);
                OfflineTrackTournamentLeaderboardTab offlineTab =
                    new(_trackTournamentGraphqlService, tournaments[i]);
                LeaderboardApi.InsertTab(FirstTournamentTabIndex + i, onlineTab);
                LeaderboardApi.InsertTab(FirstTournamentTabIndex + i, offlineTab);
                _tournamentTabs.Add(onlineTab);
                _tournamentTabs.Add(offlineTab);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            if (generation == _generation)
                _levelKey = null;
            _logger.LogWarning(e, "Failed to load track tournaments for current level");
        }
    }

    private void ClearTournamentTabs()
    {
        _generation++;
        _lookupCancellationTokenSource?.Cancel();
        _lookupCancellationTokenSource?.Dispose();
        _lookupCancellationTokenSource = null;
        _levelKey = null;

        foreach (TrackTournamentLeaderboardTabBase tab in _tournamentTabs)
        {
            LeaderboardApi.RemoveTab(tab);
            tab.Dispose();
        }

        _tournamentTabs.Clear();
    }

    public void Dispose()
    {
        RacingApi.LevelLoaded -= RefreshTournamentTabs;
        RacingApi.PlayerSpawned -= EnsureTournamentTabs;
        RacingApi.Quit -= ClearTournamentTabs;
        ClearTournamentTabs();
    }
}
