using System;
using System.Collections.Generic;
using System.Threading;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
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

public class OfflineLeaderboardTab : BaseSingleplayerLeaderboardTab
{
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
    }

    protected override string GetLeaderboardTitle()
    {
        return "GTR Records";
    }

    protected override void OnEnable()
    {
        for (int i = 0; i < 16; i++)
        {
            int index = i;
            GUI_OnlineLeaderboardPosition instance = Instance.leaderboard_tab_positions[index];
            _originalEvents[index] = instance.favoriteButton.onClick;
            instance.favoriteButton.onClick = new UnityEvent();
            instance.favoriteButton.onClick.AddListener(() => OnFavoriteButtonClicked(index));
        }

        InitializeAsync().Forget();
    }

    protected override void OnDisable()
    {
        for (int i = 0; i < 16; i++)
        {
            Instance.leaderboard_tab_positions[i].favoriteButton.onClick = _originalEvents[i];
        }
    }

    protected override void OnDraw()
    {
        for (int i = 0; i < Instance.leaderboard_tab_positions.Count; i++)
        {
            GUI_OnlineLeaderboardPosition gui = Instance.leaderboard_tab_positions[i];
            if (i >= _items.Count)
            {
                continue;
            }

            gui.gameObject.SetActive(true);
            IGetPersonalBests_Records_Nodes item = _items[i];
            int index = CurrentPage * Instance.leaderboard_tab_positions.Count + i;
            OnDrawItem(gui, item, index);
        }
    }

    protected override void OnPageChanged(int previous, int current)
    {
        LoadRecords(current);
    }

    private void OnFavoriteButtonClicked(int i)
    {
        GUI_OnlineLeaderboardPosition instance = Instance.leaderboard_tab_positions[i];
        instance.isFavorite = !instance.isFavorite;

        IGetPersonalBests_Records_Nodes node = _items[i];

        if (instance.isFavorite)
        {
            _offlineGhostsService.AddAdditionalGhost(node.User.SteamId);
        }
        else
        {
            _offlineGhostsService.RemoveAdditionalGhost(node.User.SteamId);
        }

        instance.RedrawFavoriteImage();
    }

    private async UniTaskVoid InitializeAsync()
    {
        Result<int?> levelPointsResult = await _graphqlService.GetLevelPoints(LevelApi.CurrentHash);
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
        
        Result<int> personalBestCount = await _graphqlService.GetPersonalBestCount(LevelApi.CurrentHash);
        if (personalBestCount.IsFailed)
        {
            Logger.LogError("Failed to get count");
            personalBestCount.NotifyErrors();
            return;
        }

        _personalBests = personalBestCount.Value;
        MaxPages = _personalBests / Instance.leaderboard_tab_positions.Count;
        UpdatePageNumber();

        LoadRecords(CurrentPage);
    }

    private void LoadRecords(int page)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        LoadRecords(page, _cancellationTokenSource.Token).Forget();
    }

    private async UniTaskVoid LoadRecords(int page, CancellationToken ct)
    {
        _items.Clear();
        Draw();

        UniTask<Result<IGetPersonalBestsResult>> recordsTask =
            _graphqlService.GetLeaderboardRecords(LevelApi.CurrentHash, page, ct);
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

    protected void OnDrawItem(GUI_OnlineLeaderboardPosition gui, IGetPersonalBests_Records_Nodes item, int index)
    {
        gui.position.gameObject.SetActive(true);
        gui.position.text = (index + 1).ToString();
        gui.position.color = PlayerManager.Instance.GetColorFromPosition(index + 1);
        gui.favoriteButton.gameObject.SetActive(true);
        gui.isFavorite = _offlineGhostsService.ContainsAdditionalGhost(item.User.SteamId);
        gui.RedrawFavoriteImage();
        if (PlayerManager.Instance.steamAchiever &&
            PlayerManager.Instance.steamAchiever.GetPlayerSteamID().ToString() == item.User.SteamId)
        {
            string playerColor = ColorUtility.ToHtmlStringRGB(PlayerManager.Instance.GetChatColor());
            gui.player_name.text =
                $"<color=#{playerColor}><link=\"{item.User.SteamId}\">{item.User.SteamName}</link></color>";
        }
        else
            gui.player_name.text = $"<link=\"{item.User.SteamId}\">{item.User.SteamName}</link>";

        gui.time.text = item.Time.GetFormattedTime();
        gui.pointsWon.gameObject.SetActive(_levelPoints.HasValue);
        if (_levelPoints.HasValue)
        {
            gui.pointsWon.text = $"(+{(int)Math.Round(_levelPoints.Value * Math.Pow(0.95, index))})";
        }
    }
}
