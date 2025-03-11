using System;
using System.Collections.Generic;
using System.Threading;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Messaging;
using UnityEngine.Events;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Leaderboard.Pages;
using ZeepSDK.Level;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class OfflineLeaderboardTab : BaseSingleplayerLeaderboardTab<LeaderboardRecord>
{
    private readonly LeaderboardGraphqlService _graphqlService;
    private readonly MessengerService _messengerService;
    private readonly OfflineGhostsService _offlineGhostsService;
    private readonly UnityEvent[] _originalEvents = new UnityEvent[16];

    private static readonly Dictionary<int, double> Fibbonus = new()
    {
        { 0, 0.21 },
        { 1, 0.13 },
        { 2, 0.08 },
        { 3, 0.05 },
        { 4, 0.03 },
        { 5, 0.02 },
        { 6, 0.01 },
        { 7, 0.01 }
    };

    private double? _levelPoints;
    private int _totalUsers;
    private CancellationTokenSource _cts;

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

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        LoadRecords(_cts.Token).Forget();
    }

    protected override void OnDisable()
    {
        for (int i = 0; i < 16; i++)
        {
            Instance.leaderboard_tab_positions[i].favoriteButton.onClick = _originalEvents[i];
        }
    }

    private void OnFavoriteButtonClicked(int i)
    {
        int j = CurrentPage * 16 + i;

        GUI_OnlineLeaderboardPosition instance = Instance.leaderboard_tab_positions[i];
        instance.isFavorite = !instance.isFavorite;

        LeaderboardRecord node = ElementAt(j);

        if (instance.isFavorite)
        {
            _offlineGhostsService.AddAdditionalGhost(node.SteamId);
        }
        else
        {
            _offlineGhostsService.RemoveAdditionalGhost(node.SteamId);
        }

        instance.RedrawFavoriteImage();
    }

    private async UniTaskVoid LoadRecords(CancellationToken ct = default)
    {
        ClearItems();
        Draw();
        Result<LeaderboardRecords> result = await _graphqlService.GetLeaderboardRecords(LevelApi.CurrentHash, ct);

        if (ct.IsCancellationRequested)
            return;

        if (result.IsFailed)
        {
            Logger.LogError("Failed to load GTR records: " + result);
            _messengerService.LogError("Failed to load GTR records");
            return;
        }

        _levelPoints = result.Value.LevelPoints == 0 ? null : result.Value.LevelPoints;
        _totalUsers = result.Value.TotalUsers;
        ClearItems();
        AddItems(result.Value.Records);
        SortItems((x, y) => x.Time.CompareTo(y.Time));
        Draw();
    }

    protected override void OnDrawItem(GUI_OnlineLeaderboardPosition gui, LeaderboardRecord item, int index)
    {
        gui.position.gameObject.SetActive(true);
        gui.position.text = (index + 1).ToString();
        gui.position.color = PlayerManager.Instance.GetColorFromPosition(index + 1);
        gui.favoriteButton.gameObject.SetActive(true);
        gui.isFavorite = _offlineGhostsService.ContainsAdditionalGhost(item.SteamId);
        gui.RedrawFavoriteImage();
        if (PlayerManager.Instance.steamAchiever && PlayerManager.Instance.steamAchiever.GetPlayerSteamID().ToString() == item.SteamId)
        {
            string playerColor = UnityEngine.ColorUtility.ToHtmlStringRGB(PlayerManager.Instance.GetChatColor());
            gui.player_name.text = $"<color=#{playerColor}><link=\"{item.SteamId}\">{item.SteamName}</link></color>";
        }
        else
            gui.player_name.text = $"<link=\"{item.SteamId}\">{item.SteamName}</link>";
        gui.time.text = item.Time.GetFormattedTime();

        int placementPoints = Math.Max(0, Count - index);
        double a = 1d / (_totalUsers / (double)Count);
        int b = index + 1;
        double c = index < 8 ? Fibbonus[index] : 0;
        double points = placementPoints * (1 + a / b) + c;

        gui.pointsWon.gameObject.SetActive(_levelPoints.HasValue);
        gui.pointsWon.text = $"(+{(int)Math.Round(points)})";
    }
}
