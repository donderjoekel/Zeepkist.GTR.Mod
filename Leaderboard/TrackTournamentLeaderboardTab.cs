using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;
using ZeepkistClient;
using ZeepSDK.Extensions;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Leaderboard;
using ZeepSDK.Leaderboard.Pages;
using ZeepSDK.Multiplayer;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public abstract class TrackTournamentLeaderboardTabBase : BaseCoreLeaderboardTab, IDisposable
{
    private const int PageSize = 16;

    private readonly TrackTournamentGraphqlService _graphqlService;
    private readonly List<TrackTournamentStanding> _items = [];

    private TrackTournamentDescriptor _tournament;
    private IDisposable _subscription;
    private CancellationTokenSource _titleCancellationTokenSource;
    private int _generation;

    protected TrackTournamentLeaderboardTabBase(
        TrackTournamentGraphqlService graphqlService,
        TrackTournamentDescriptor tournament)
    {
        _graphqlService = graphqlService;
        _tournament = tournament;
        MultiplayerApi.DisconnectedFromGame += StopForContextChange;
    }

    protected override string GetLeaderboardTitle()
    {
        return TrackTournamentTextFormatter.FormatTitle(_tournament, DateTimeOffset.Now);
    }

    protected override void OnEnable()
    {
        LoadPage(CurrentPage);
        ScheduleTitleRefresh();
    }

    protected override void OnDisable()
    {
        StopPage();
        StopTitleRefresh();
    }

    protected override void OnDraw()
    {
        for (int i = 0; i < Instance.leaderboard_tab_positions.Count; i++)
        {
            if (i >= _items.Count)
                continue;

            GUI_OnlineLeaderboardPosition gui = Instance.leaderboard_tab_positions[i];
            gui.gameObject.SetActive(true);
            DrawItem(gui, _items[i]);
        }
    }

    protected override void OnPageChanged(int previous, int current)
    {
        LoadPage(current);
    }

    private void LoadPage(int page)
    {
        StopPage();
        int generation = ++_generation;
        _subscription = _graphqlService.WatchPage(
            _tournament,
            page,
            PageSize,
            snapshot => ApplySnapshotAsync(snapshot, generation).Forget(),
            error => Logger.LogWarning("Track tournament leaderboard subscription failed: " + error));
    }

    private async UniTaskVoid ApplySnapshotAsync(TrackTournamentPageSnapshot snapshot, int generation)
    {
        await UniTask.SwitchToMainThread();
        if (generation != _generation || snapshot == null)
            return;

        _tournament = snapshot.Tournament;
        _items.Clear();
        _items.AddRange(snapshot.Records);
        MaxPages = LeaderboardPagination.GetMaxPageIndex(snapshot.TotalRecords, PageSize);
        UpdatePageNumber();
        Instance.leaderboardTitle.text = GetLeaderboardTitle();
        ScheduleTitleRefresh();
        Draw();
    }

    private static void DrawItem(GUI_OnlineLeaderboardPosition gui, TrackTournamentStanding item)
    {
        gui.position.gameObject.SetActive(true);
        gui.position.text = item.Rank.ToString(CultureInfo.InvariantCulture);
        gui.position.color = PlayerManager.Instance.GetColorFromPosition(item.Rank);
        gui.favoriteButton.gameObject.SetActive(false);
        gui.pointsCurrent.gameObject.SetActive(false);
        gui.pointsWon.gameObject.SetActive(true);

        gui.player_name.text = FormatPlayer(gui, item);
        gui.time.text = item.Time.GetFormattedTime();
        gui.pointsWon.text = item.Points.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatPlayer(GUI_OnlineLeaderboardPosition gui, TrackTournamentStanding item)
    {
        string name = LeaderboardTextFormatter.EscapeRichText(
            string.IsNullOrWhiteSpace(item.SteamName) ? "Unknown player" : item.SteamName);
        string link = string.IsNullOrWhiteSpace(item.SteamId)
            ? name
            : $"<link=\"{item.SteamId}\">{name}</link>";

        if (ZeepkistNetwork.IsConnectedToGame)
        {
            gui.thePlayer = null;
            if (ulong.TryParse(item.SteamId, out ulong steamId))
                ZeepkistNetwork.TryGetPlayer(steamId, out gui.thePlayer);

            if (ZeepkistNetwork.LocalPlayer.SteamID.ToString() == item.SteamId)
            {
                string color = ColorUtility.ToHtmlStringRGB(ZeepkistNetwork.LocalPlayer.chatColor);
                return $"<color=#{color}>{link}</color>";
            }

            if (gui.thePlayer != null && gui.thePlayer.SteamID.ToString() == item.SteamId)
            {
                string color = ColorUtility.ToHtmlStringRGB(gui.thePlayer.chatColor);
                return $"<color=#{color}>{link}</color>";
            }
        }
        else if (PlayerManager.Instance.steamAchiever &&
                 PlayerManager.Instance.steamAchiever.GetPlayerSteamID().ToString() == item.SteamId)
        {
            string color = ColorUtility.ToHtmlStringRGB(PlayerManager.Instance.GetChatColor());
            return $"<color=#{color}>{link}</color>";
        }

        return link;
    }

    private void ScheduleTitleRefresh()
    {
        StopTitleRefresh();
        TimeSpan delay = _tournament.EndAt - DateTimeOffset.Now;
        if (delay <= TimeSpan.Zero)
            return;

        int generation = _generation;
        _titleCancellationTokenSource = new CancellationTokenSource();
        RefreshTitleAtEndAsync(delay, generation, _titleCancellationTokenSource.Token).Forget();
    }

    private async UniTaskVoid RefreshTitleAtEndAsync(
        TimeSpan delay,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.Delay(delay, cancellationToken: cancellationToken);
            await UniTask.SwitchToMainThread(cancellationToken);
            if (generation == _generation && IsActive)
                Instance.leaderboardTitle.text = GetLeaderboardTitle();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void StopTitleRefresh()
    {
        _titleCancellationTokenSource?.Cancel();
        _titleCancellationTokenSource?.Dispose();
        _titleCancellationTokenSource = null;
    }

    private void StopPage()
    {
        _generation++;
        _subscription?.Dispose();
        _subscription = null;
    }

    private void StopForContextChange()
    {
        StopPage();
        StopTitleRefresh();
        _items.Clear();
    }

    public void Dispose()
    {
        MultiplayerApi.DisconnectedFromGame -= StopForContextChange;
        StopForContextChange();
    }
}

public sealed class OnlineTrackTournamentLeaderboardTab : TrackTournamentLeaderboardTabBase,
    IMultiplayerLeaderboardTab
{
    public OnlineTrackTournamentLeaderboardTab(
        TrackTournamentGraphqlService graphqlService,
        TrackTournamentDescriptor tournament)
        : base(graphqlService, tournament)
    {
    }
}

public sealed class OfflineTrackTournamentLeaderboardTab : TrackTournamentLeaderboardTabBase,
    ISingleplayerLeaderboardTab
{
    public OfflineTrackTournamentLeaderboardTab(
        TrackTournamentGraphqlService graphqlService,
        TrackTournamentDescriptor tournament)
        : base(graphqlService, tournament)
    {
    }
}
