using System;
using System.Collections.Generic;
using TNRD.Zeepkist.GTR.GraphQL;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;
using ZeepkistClient;
using ZeepSDK.Extensions;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Leaderboard.Pages;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class OnlineLeaderboardTab : BaseMultiplayerLeaderboardTab, IDisposable
{
    private readonly LeaderboardGraphqlService _graphqlService;
    private readonly List<LeaderboardRecord> _items = [];

    private IDisposable _subscription;
    private int _generation;
    private string _title = "GTR Records";

    public OnlineLeaderboardTab(LeaderboardGraphqlService graphqlService)
    {
        _graphqlService = graphqlService;
        RacingApi.LevelLoaded += StopForContextChange;
        RacingApi.Quit += StopForContextChange;
        MultiplayerApi.DisconnectedFromGame += StopForContextChange;
    }

    protected override string GetLeaderboardTitle()
    {
        return _title;
    }

    protected override void OnEnable()
    {
        _title = "GTR Records";
        LoadPage(CurrentPage);
    }

    protected override void OnDisable()
    {
        StopPage();
    }

    protected override void OnDraw()
    {
        for (int i = 0; i < Instance.leaderboard_tab_positions.Count; i++)
        {
            GUI_OnlineLeaderboardPosition gui = Instance.leaderboard_tab_positions[i];
            if (i >= _items.Count)
                continue;

            gui.gameObject.SetActive(true);
            int index = CurrentPage * Instance.leaderboard_tab_positions.Count + i;
            OnDrawItem(gui, _items[i], index);
        }
    }

    protected override void OnPageChanged(int previous, int current)
    {
        LoadPage(current);
    }

    private void LoadPage(int page)
    {
        StopPage();
        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        if (!level.IsAvailable)
            return;

        int generation = ++_generation;
        int pageSize = Instance.leaderboard_tab_positions.Count;
        _subscription = _graphqlService.WatchPage(
            level,
            page,
            pageSize,
            snapshot => ApplySnapshotAsync(snapshot, generation).Forget(),
            error => Logger.LogWarning("GTR leaderboard subscription failed: " + error));
    }

    private async UniTaskVoid ApplySnapshotAsync(LeaderboardPageSnapshot snapshot, int generation)
    {
        await UniTask.SwitchToMainThread();
        if (generation != _generation)
            return;
        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(LeaderboardPageSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        _items.Clear();
        _items.AddRange(snapshot.Records);
        MaxPages = LeaderboardPagination.GetMaxPageIndex(
            snapshot.TotalRecords,
            Instance.leaderboard_tab_positions.Count);
        UpdatePageNumber();
        _title = LeaderboardTextFormatter.FormatTitle(snapshot.LevelName);
        Instance.leaderboardTitle.text = _title;
        Draw();
    }

    private void OnDrawItem(GUI_OnlineLeaderboardPosition gui, LeaderboardRecord item, int index)
    {
        ZeepkistNetwork.TryGetPlayer(Convert.ToUInt64(item.SteamId), out gui.thePlayer);

        gui.position.gameObject.SetActive(true);
        int position = item.LevelPosition ?? index + 1;
        gui.position.text = position.ToString();
        gui.position.color = PlayerManager.Instance.GetColorFromPosition(position);
        gui.favoriteButton.gameObject.SetActive(false);

        string playerMarkup;
        if (ZeepkistNetwork.LocalPlayer.SteamID.ToString() == item.SteamId)
        {
            string playerColor = ColorUtility.ToHtmlStringRGB(ZeepkistNetwork.LocalPlayer.chatColor);
            playerMarkup = $"<color=#{playerColor}><link=\"{item.SteamId}\">{item.SteamName}</link></color>";
        }
        else if (gui.thePlayer != null && gui.thePlayer.SteamID.ToString() == item.SteamId)
        {
            string playerColor = ColorUtility.ToHtmlStringRGB(gui.thePlayer.chatColor);
            playerMarkup = $"<color=#{playerColor}><link=\"{item.SteamId}\">{item.SteamName}</link></color>";
        }
        else
        {
            playerMarkup = $"<link=\"{item.SteamId}\">{item.SteamName}</link>";
        }

        gui.player_name.text = playerMarkup;
        gui.time.text = item.Time.GetFormattedTime();
        gui.pointsWon.gameObject.SetActive(true);

        string pointsMarkup = LeaderboardTextFormatter.PrefixRecordDate(
            item.LevelDecayedPoints.HasValue ? Math.Round(item.LevelDecayedPoints.Value).ToString() : string.Empty,
            item.DateCreated,
            DateTimeOffset.Now
        );

        gui.pointsWon.text = pointsMarkup;
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
        _items.Clear();
        _title = "GTR Records";
    }

    public void Dispose()
    {
        RacingApi.LevelLoaded -= StopForContextChange;
        RacingApi.Quit -= StopForContextChange;
        MultiplayerApi.DisconnectedFromGame -= StopForContextChange;
        StopPage();
    }
}
