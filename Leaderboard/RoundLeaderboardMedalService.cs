using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Steamworks;
using StrawberryShake;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.GraphQL;
using TNRD.Zeepkist.GTR.Patching.Patches;
using TNRD.Zeepkist.GTR.UI.Medals;
using ZeepkistClient;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Leaderboard.Pages;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public sealed class RoundLeaderboardMedalService : IEagerService, IDisposable
{
    private readonly IGtrClient _client;
    private readonly ILogger<RoundLeaderboardMedalService> _logger;
    private readonly RoundMedalState _state = new();
    private readonly HashSet<GUI_OnlineLeaderboardPosition> _ownedRows = new();
    private CancellationTokenSource _lookupCancellation;
    private BaseCoreLeaderboardTab _tab;
    private OnlineTabLeaderboardUI _ui;
    private int _tabGeneration;

    public RoundLeaderboardMedalService(IGtrClient client, ILogger<RoundLeaderboardMedalService> logger)
    {
        _client = client;
        _logger = logger;
        RoundLeaderboardMedals.TabEnabled += OnTabEnabled;
        RoundLeaderboardMedals.TabDisabled += OnTabDisabled;
        RoundLeaderboardMedals.RoundDrawn += OnRoundDrawn;
        RacingApi.LevelLoaded += OnLevelLoaded;
        RacingApi.PlayerSpawned += EnsureLevel;
        RacingApi.Quit += Stop;
        MultiplayerApi.DisconnectedFromGame += Stop;
        ZeepkistNetwork.LeaderboardUpdated += OnLeaderboardUpdated;
        ZeepkistNetwork.PlayerResultsChanged += OnPlayerResultsChanged;
    }

    private bool OwnsLeaderboard => _tab != null && _ui != null &&
        RoundMedalState.IsOfficialTab(_tab.GetType().FullName, _ui.drawChampionShipLeaderboard) &&
        ZeepkistNetwork.IsConnectedToGame;

    private void OnTabEnabled(BaseCoreLeaderboardTab tab, OnlineTabLeaderboardUI ui)
    {
        EndRound();
        if (!RoundMedalState.IsOfficialTab(tab.GetType().FullName, ui.drawChampionShipLeaderboard) ||
            !ZeepkistNetwork.IsConnectedToGame) return;
        _tab = tab;
        _ui = ui;
        _state.ResetPresentation();
        EnsureLevel();
    }

    private void OnTabDisabled(BaseCoreLeaderboardTab tab)
    {
        if (ReferenceEquals(tab, _tab)) EndRound();
    }

    private void OnRoundDrawn(OnlineTabLeaderboardUI ui)
    {
        if (!ReferenceEquals(ui, _ui) || !OwnsLeaderboard) return;
        try { ApplyRows(); }
        catch (Exception error) { _logger.LogWarning(error, "Unable to draw lobby leaderboard medals"); }
    }

    private void OnLeaderboardUpdated() => RedrawAsync(_tabGeneration).Forget();
    private void OnPlayerResultsChanged(ZeepkistNetworkPlayer _) => OnLeaderboardUpdated();

    private async UniTaskVoid RedrawAsync(int generation)
    {
        await UniTask.SwitchToMainThread();
        if (generation != _tabGeneration || !OwnsLeaderboard) return;
        // Native redraw preserves its sorting, host markup, points and ghost controls.
        _ui.ClientRedrawLeaderBoard();
    }

    private void EnsureLevel()
    {
        if (!ZeepkistNetwork.IsConnectedToGame) return;
        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        if (!level.IsAvailable || !_state.BeginLevel(level.CacheKey)) return;
        ClearRows();
        CancelLookup();
        _lookupCancellation = new CancellationTokenSource();
        LoadTargetsAsync(level, _state.Generation, _lookupCancellation).Forget();
    }

    private async UniTaskVoid LoadTargetsAsync(LevelGraphqlIdentity level, int generation, CancellationTokenSource cancellation)
    {
        try
        {
            var result = await _client.GetCurrentLevelMedalTargets.ExecuteAsync(
                level.XxHash, level.Hash, cancellation.Token);
            result.EnsureNoErrors();
            var item = result.Data?.Levels?.Nodes.FirstOrDefault()?.LevelItems?.Nodes.FirstOrDefault();
            LevelMedalTargets targets = item == null ? null : new LevelMedalTargets
            {
                Bronze = item.ValidationTimeBronze,
                Silver = item.ValidationTimeSilver,
                Gold = item.ValidationTimeGold,
                Author = item.ValidationTimeAuthor,
                Expert = item.ValidationTimeExpert,
                Apex = item.ValidationTimeApex
            };
            await UniTask.SwitchToMainThread();
            if (cancellation.IsCancellationRequested || !_state.SetTargets(generation, targets)) return;
            if (OwnsLeaderboard) OnRoundDrawn(_ui);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (generation == _state.Generation && !cancellation.IsCancellationRequested)
                _logger.LogWarning(error, "Unable to load current level medal targets");
        }
        finally
        {
            await UniTask.SwitchToMainThread();
            if (ReferenceEquals(_lookupCancellation, cancellation)) _lookupCancellation = null;
            cancellation.Dispose();
        }
    }

    private void ApplyRows()
    {
        var results = ZeepkistNetwork.Leaderboard;
        bool localVisible = false;
        foreach (GUI_OnlineLeaderboardPosition row in _ui.leaderboard_tab_positions)
        {
            if (row == null) continue;
            bool visible = row.gameObject.activeInHierarchy && row.position.gameObject.activeSelf && row.steamID != 0;
            if (!visible || _state.Targets == null)
            {
                if (_ownedRows.Remove(row)) RecordMedalUi.Clear(row.time);
                continue;
            }
            // Membership in the native result list identifies completed runs. CurrentResult also holds DNF data.
            int resultIndex = results.FindIndex(record => record.SteamID == row.steamID);
            bool completed = resultIndex >= 0;
            double? time = completed ? results[resultIndex].Time : null;
            int? medal = _state.Targets.Classify(time, completed);
            bool local = row.steamID == SteamClient.SteamId;
            MedalUpgrade upgrade = null;
            if (local)
            {
                localVisible = true;
                upgrade = _state.ObserveLocal(_ui.currentPage, true, time, completed);
            }
            RecordMedalUi.ConfigureLeaderboard(row.time, row.player_name);
            RecordMedalUi.Set(row.time, medal, upgrade, row.steamID.ToString());
            _ownedRows.Add(row);
        }
        if (!localVisible) _state.ResetPresentation();
    }

    private void ClearRows()
    {
        foreach (GUI_OnlineLeaderboardPosition row in _ownedRows)
            if (row != null) RecordMedalUi.Clear(row.time);
        _ownedRows.Clear();
    }

    private void EndRound()
    {
        _tabGeneration++;
        ClearRows();
        _state.ResetPresentation();
        _tab = null;
        _ui = null;
    }

    private void OnLevelLoaded()
    {
        ResetLevel();
        EnsureLevel();
    }

    private void CancelLookup()
    {
        _lookupCancellation?.Cancel();
        // Requests own token sources until their async continuations finish.
        _lookupCancellation = null;
    }

    private void ResetLevel()
    {
        _tabGeneration++;
        ClearRows();
        CancelLookup();
        _state.ResetLevel();
    }

    private void Stop()
    {
        EndRound();
        ResetLevel();
    }

    public void Dispose()
    {
        RoundLeaderboardMedals.TabEnabled -= OnTabEnabled;
        RoundLeaderboardMedals.TabDisabled -= OnTabDisabled;
        RoundLeaderboardMedals.RoundDrawn -= OnRoundDrawn;
        RacingApi.LevelLoaded -= OnLevelLoaded;
        RacingApi.PlayerSpawned -= EnsureLevel;
        RacingApi.Quit -= Stop;
        MultiplayerApi.DisconnectedFromGame -= Stop;
        ZeepkistNetwork.LeaderboardUpdated -= OnLeaderboardUpdated;
        ZeepkistNetwork.PlayerResultsChanged -= OnPlayerResultsChanged;
        Stop();
    }
}
