using System;
using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Messaging;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Level;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class OnlineLeaderboardTab : BaseMultiplayerLeaderboardTab<LeaderboardRecord>
{
    private readonly LeaderboardGraphqlService _graphqlService;
    private readonly MessengerService _messengerService;

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

    public OnlineLeaderboardTab(LeaderboardGraphqlService graphqlService, MessengerService messengerService)
    {
        _graphqlService = graphqlService;
        _messengerService = messengerService;
    }

    protected override string GetLeaderboardTitle()
    {
        return "GTR Records";
    }

    protected override void OnEnable()
    {
        LoadRecords().Forget();
    }

    protected override void OnDisable()
    {
    }

    private async UniTaskVoid LoadRecords()
    {
        string levelHash = LevelApi.GetLevelHash(LevelApi.CurrentLevel);
        Result<LeaderboardRecords> result = await _graphqlService.GetLeaderboardRecords(levelHash);

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
        gui.favoriteButton.gameObject.SetActive(false);
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
