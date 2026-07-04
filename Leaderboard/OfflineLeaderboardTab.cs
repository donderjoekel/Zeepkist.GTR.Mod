using System;
using System.Collections.Generic;
using System.Threading;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.GraphQL;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;
using UnityEngine.Events;
using ZeepSDK.Extensions;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Leaderboard.Pages;
using ZeepSDK.Level;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class OfflineLeaderboardTab : BaseSingleplayerLeaderboardTab, IDisposable
{
    private const int PageSize = 14;

    private readonly LeaderboardGraphqlService _graphqlService;
    private readonly MessengerService _messengerService;
    private readonly OfflineGhostsService _offlineGhostsService;
    private readonly UnityEvent[] _originalEvents = new UnityEvent[16];
    private readonly List<IGetPersonalBests_Records_Nodes> _items = [];

    private int _personalBests;
    private int? _levelPoints;
    private CancellationTokenSource _cancellationTokenSource;

    public OfflineLeaderboardTab(
        LeaderboardGraphqlService graphqlService,
        MessengerService messengerService,
        OfflineGhostsService offlineGhostsService)
    {
        _graphqlService = graphqlService;
        _messengerService = messengerService;
        _offlineGhostsService = offlineGhostsService;
        _offlineGhostsService.BulkModeChanged += OnBulkModeChanged;
    }

    protected override string GetLeaderboardTitle()
    {
        return "GTR Records";
    }

    protected override void OnEnable()
    {
        for (int i = 0; i < 16; i++)
        {
            int rowIndex = i;
            GUI_OnlineLeaderboardPosition row = Instance.leaderboard_tab_positions[rowIndex];
            _originalEvents[rowIndex] = row.favoriteButton.onClick;
            row.favoriteButton.onClick = new UnityEvent();
            row.favoriteButton.onClick.AddListener(
                rowIndex switch
                {
                    0 => OnShowAllGhostsClicked,
                    1 => OnShowTopRecordsClicked,
                    _ => () => OnFavoriteButtonClicked(rowIndex - 2)
                });
        }

        InitializeAsync().Forget();
    }

    protected override void OnDisable()
    {
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
            IGetPersonalBests_Records_Nodes item = _items[itemIndex];
            int recordIndex = CurrentPage * PageSize + itemIndex;
            OnDrawItem(gui, item, recordIndex);
        }
    }

    protected override void OnPageChanged(int previous, int current)
    {
        LoadRecords(current);
    }

    private void OnFavoriteButtonClicked(int itemIndex)
    {
        if (itemIndex >= _items.Count)
            return;

        GUI_OnlineLeaderboardPosition row = Instance.leaderboard_tab_positions[itemIndex + 2];
        row.isFavorite = !row.isFavorite;
        IGetPersonalBests_Records_Nodes node = _items[itemIndex];

        if (row.isFavorite)
            _offlineGhostsService.AddAdditionalGhost(node.User.SteamId);
        else
            _offlineGhostsService.RemoveAdditionalGhost(node.User.SteamId);

        row.RedrawFavoriteImage();
    }

    private async UniTaskVoid InitializeAsync()
    {
        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        if (!level.IsAvailable)
            return;

        Result<int?> levelPointsResult = await _graphqlService.GetLevelPoints(level);
        if (levelPointsResult.IsFailed)
        {
            Logger.LogError("Failed to get level points");
            levelPointsResult.NotifyErrors();
            _levelPoints = null;
        }
        else
        {
            _levelPoints = levelPointsResult.Value;
        }

        Result<int> personalBestCount = await _graphqlService.GetPersonalBestCount(level);
        if (personalBestCount.IsFailed)
        {
            Logger.LogError("Failed to get count");
            personalBestCount.NotifyErrors();
            return;
        }

        _personalBests = personalBestCount.Value;
        MaxPages = Math.Max(0, (_personalBests - 1) / PageSize);
        UpdatePageNumber();
        LoadRecords(CurrentPage);
    }

    private void LoadRecords(int page)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        LoadRecords(page, _cancellationTokenSource.Token).Forget();
    }

    private async UniTaskVoid LoadRecords(int page, CancellationToken ct)
    {
        _items.Clear();
        Draw();

        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        if (!level.IsAvailable)
            return;

        UniTask<Result<IGetPersonalBestsResult>> recordsTask =
            _graphqlService.GetLeaderboardRecords(level, page, ct, PageSize);
        UniTask<Result<int>> userCountTask = _graphqlService.GetTotalUserCount(ct);

        (Result<IGetPersonalBestsResult> recordsResult, Result<int> userCountResult) =
            await UniTask.WhenAll(recordsTask, userCountTask);

        if (ct.IsCancellationRequested)
            return;

        if (recordsResult.IsFailed)
        {
            Logger.LogError("Failed to load GTR records: " + recordsResult);
            _messengerService.LogError("Failed to load GTR records");
            return;
        }

        if (userCountResult.IsFailed)
        {
            Logger.LogError("Failed to load GTR records: " + userCountResult);
            _messengerService.LogError("Failed to load GTR records");
            return;
        }

        _items.Clear();
        _items.AddRange(recordsResult.Value.Records.Nodes);
        Draw();
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
        gui.player_name.text =
            _offlineGhostsService.IsShowingAllGhosts ? "Clear Personal Bests" : "Show All Personal Bests";
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

    private void OnDrawItem(GUI_OnlineLeaderboardPosition gui, IGetPersonalBests_Records_Nodes item, int index)
    {
        gui.position.gameObject.SetActive(true);
        gui.position.text = (index + 1).ToString();
        gui.position.color = PlayerManager.Instance.GetColorFromPosition(index + 1);
        gui.favoriteButton.gameObject.SetActive(true);
        gui.favoriteButton.disabled = false;
        gui.isFavorite = _offlineGhostsService.ContainsAdditionalGhost(item.User.SteamId);
        gui.RedrawFavoriteImage();
        gui.favoriteButton.RedrawButton();
        if (PlayerManager.Instance.steamAchiever &&
            PlayerManager.Instance.steamAchiever.GetPlayerSteamID().ToString() == item.User.SteamId)
        {
            string playerColor = ColorUtility.ToHtmlStringRGB(PlayerManager.Instance.GetChatColor());
            gui.player_name.text =
                $"<color=#{playerColor}><link=\"{item.User.SteamId}\">{item.User.SteamName}</link></color>";
        }
        else
        {
            gui.player_name.text = $"<link=\"{item.User.SteamId}\">{item.User.SteamName}</link>";
        }

        gui.time.text = item.Time.GetFormattedTime();
        gui.pointsCurrent.gameObject.SetActive(false);
        gui.pointsWon.gameObject.SetActive(_levelPoints.HasValue);
        if (_levelPoints.HasValue)
            gui.pointsWon.text = $"(+{(int)Math.Round(_levelPoints.Value * Math.Pow(0.985, index))})";
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

    public void Dispose()
    {
        _offlineGhostsService.BulkModeChanged -= OnBulkModeChanged;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}
