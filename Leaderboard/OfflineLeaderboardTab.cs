using System;
using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.GraphQL;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;
using UnityEngine.Events;
using ZeepSDK.Extensions;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Leaderboard.Pages;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class OfflineLeaderboardTab : BaseSingleplayerLeaderboardTab, IDisposable
{
    private const int PageSize = 14;

    private readonly LeaderboardGraphqlService _graphqlService;
    private readonly OfflineGhostsService _offlineGhostsService;
    private readonly UnityEvent[] _originalEvents = new UnityEvent[16];
    private readonly List<LeaderboardRecord> _items = [];

    private int _generation;
    private string _title = "GTR Records";
    private IDisposable _subscription;

    public OfflineLeaderboardTab(
        LeaderboardGraphqlService graphqlService,
        OfflineGhostsService offlineGhostsService)
    {
        _graphqlService = graphqlService;
        _offlineGhostsService = offlineGhostsService;
        _offlineGhostsService.BulkModeChanged += OnBulkModeChanged;
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
        for (int i = 0; i < 16; i++)
        {
            int rowIndex = i;
            GUI_OnlineLeaderboardPosition row = Instance.leaderboard_tab_positions[rowIndex];
            _originalEvents[rowIndex] = row.favoriteButton.onClick;
            row.favoriteButton.onClick = new UnityEvent();
            row.favoriteButton.onClick.AddListener(rowIndex switch
            {
                0 => OnShowAllGhostsClicked,
                1 => OnShowTopRecordsClicked,
                _ => () => OnFavoriteButtonClicked(rowIndex - 2)
            });
        }

        LoadPage(CurrentPage);
    }

    protected override void OnDisable()
    {
        StopPage();
        for (int i = 0; i < 16; i++)
            Instance.leaderboard_tab_positions[i].favoriteButton.onClick = _originalEvents[i];
    }

    protected override void OnDraw()
    {
        DrawShowAllGhostsRow(Instance.leaderboard_tab_positions[0]);
        DrawShowTopRecordsRow(Instance.leaderboard_tab_positions[1]);

        for (int rowIndex = 2; rowIndex < Instance.leaderboard_tab_positions.Count; rowIndex++)
        {
            int itemIndex = rowIndex - 2;
            if (itemIndex >= _items.Count)
                continue;

            GUI_OnlineLeaderboardPosition gui = Instance.leaderboard_tab_positions[rowIndex];
            gui.gameObject.SetActive(true);
            int recordIndex = CurrentPage * PageSize + itemIndex;
            OnDrawItem(gui, _items[itemIndex], recordIndex);
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
        _subscription = _graphqlService.WatchPage(
            level,
            page,
            PageSize,
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
        MaxPages = LeaderboardPagination.GetMaxPageIndex(snapshot.TotalRecords, PageSize);
        UpdatePageNumber();
        _title = LeaderboardTextFormatter.FormatTitle(snapshot.LevelName);
        Instance.leaderboardTitle.text = _title;
        Draw();
    }

    private void OnFavoriteButtonClicked(int itemIndex)
    {
        if (itemIndex >= _items.Count)
            return;

        GUI_OnlineLeaderboardPosition row = Instance.leaderboard_tab_positions[itemIndex + 2];
        row.isFavorite = !row.isFavorite;
        LeaderboardRecord node = _items[itemIndex];
        if (row.isFavorite)
            _offlineGhostsService.AddAdditionalGhost(node.SteamId);
        else
            _offlineGhostsService.RemoveAdditionalGhost(node.SteamId);
        row.RedrawFavoriteImage();
    }

    private void DrawShowAllGhostsRow(GUI_OnlineLeaderboardPosition gui)
    {
        gui.gameObject.SetActive(true);
        gui.position.gameObject.SetActive(true);
        gui.position.text = string.Empty;
        gui.favoriteButton.gameObject.SetActive(true);
        gui.favoriteButton.disabled = false;
        gui.isFavorite = _offlineGhostsService.IsShowingAllGhosts;
        gui.RedrawFavoriteImage();
        gui.favoriteButton.RedrawButton();
        gui.player_name.text = _offlineGhostsService.IsShowingAllGhosts
            ? "Clear Personal Bests"
            : "Show All Personal Bests";
        gui.time.text = string.Empty;
        gui.pointsCurrent.gameObject.SetActive(false);
        gui.pointsWon.gameObject.SetActive(false);
    }

    private void DrawShowTopRecordsRow(GUI_OnlineLeaderboardPosition gui)
    {
        gui.gameObject.SetActive(true);
        gui.position.gameObject.SetActive(true);
        gui.position.text = string.Empty;
        gui.favoriteButton.gameObject.SetActive(true);
        gui.favoriteButton.disabled = false;
        gui.isFavorite = _offlineGhostsService.IsShowingTopRecords;
        gui.RedrawFavoriteImage();
        gui.favoriteButton.RedrawButton();
        gui.player_name.text = _offlineGhostsService.IsShowingTopRecords
            ? "Clear Top Records"
            : $"Show Top {_offlineGhostsService.TopRecordLimit} Records";
        gui.time.text = string.Empty;
        gui.pointsCurrent.gameObject.SetActive(false);
        gui.pointsWon.gameObject.SetActive(false);
    }

    private void OnDrawItem(GUI_OnlineLeaderboardPosition gui, LeaderboardRecord item, int index)
    {
        gui.position.gameObject.SetActive(true);
        int position = item.LevelPosition ?? index + 1;
        gui.position.text = position.ToString();
        gui.position.color = PlayerManager.Instance.GetColorFromPosition(position);
        gui.favoriteButton.gameObject.SetActive(true);
        gui.favoriteButton.disabled = false;
        gui.isFavorite = _offlineGhostsService.ContainsAdditionalGhost(item.SteamId);
        gui.RedrawFavoriteImage();
        gui.favoriteButton.RedrawButton();

        string playerMarkup;
        if (PlayerManager.Instance.steamAchiever &&
            PlayerManager.Instance.steamAchiever.GetPlayerSteamID().ToString() == item.SteamId)
        {
            string playerColor = ColorUtility.ToHtmlStringRGB(PlayerManager.Instance.GetChatColor());
            playerMarkup = $"<color=#{playerColor}><link=\"{item.SteamId}\">{item.SteamName}</link></color>";
        }
        else
        {
            playerMarkup = $"<link=\"{item.SteamId}\">{item.SteamName}</link>";
        }

        gui.player_name.text = playerMarkup;
        gui.time.text = item.Time.GetFormattedTime();
        gui.pointsCurrent.gameObject.SetActive(false);
        gui.pointsWon.gameObject.SetActive(true);
        gui.pointsWon.text = LeaderboardTextFormatter.PrefixRecordDate(
            item.LevelDecayedPoints.HasValue ? $"(+{(int)Math.Round(item.LevelDecayedPoints.Value)})" : string.Empty,
            item.DateCreated,
            DateTimeOffset.Now);
    }

    private void OnShowAllGhostsClicked()
    {
        if (_offlineGhostsService.IsShowingAllGhosts)
            _offlineGhostsService.ClearAllGhosts();
        else
            _offlineGhostsService.ShowAllGhosts();
    }

    private void OnShowTopRecordsClicked()
    {
        if (_offlineGhostsService.IsShowingTopRecords)
            _offlineGhostsService.ClearTopRecords();
        else
            _offlineGhostsService.ShowTopRecords();
    }

    private void OnBulkModeChanged()
    {
        Draw();
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
        _offlineGhostsService.BulkModeChanged -= OnBulkModeChanged;
        RacingApi.LevelLoaded -= StopForContextChange;
        RacingApi.Quit -= StopForContextChange;
        MultiplayerApi.DisconnectedFromGame -= StopForContextChange;
        StopPage();
    }
}
