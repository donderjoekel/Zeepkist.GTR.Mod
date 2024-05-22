using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.Mod.Components.Ghosting;
using TNRD.Zeepkist.GTR.SDK.Extensions;
using UnityEngine.Events;
using ZeepkistClient;
using ZeepSDK.Leaderboard;
using ZeepSDK.Leaderboard.Pages;
using ZeepSDK.Level;

namespace TNRD.Zeepkist.GTR.Mod.Leaderboard;

internal class GtrLeaderboardTab : BaseCoreLeaderboardTab, IMultiplayerLeaderboardTab, ISingleplayerLeaderboardTab
{
    private const int AMOUNT_PER_PAGE = 16;

    private static Dictionary<int, double> fibbonus = new()
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

    private static UnityEvent[] originalEvents = new UnityEvent[AMOUNT_PER_PAGE];

    private static string LevelHash => ZeepkistNetwork.IsConnectedToGame
        ? InternalLevelApi.CurrentLevelHash
        : LevelApi.GetLevelHash(PlayerManager.Instance.currentMaster.GlobalLevel);

    // private readonly List<ListItem> listItems = new();
    // private readonly List<PersonalBestsRoot.Data.AllPersonalBests.Node> nodes = [];
    private readonly List<PersonalBest> nodes = new();
    private LevelPointsResponseModel levelPoints;
    private int totalUsers;

    /// <inheritdoc />
    protected override string GetLeaderboardTitle()
    {
        return "GTR Records";
    }

    /// <inheritdoc />
    protected override void OnEnable()
    {
        LoadRecords().Forget();

        if (ZeepkistNetwork.IsConnectedToGame)
            return;

        for (int i = 0; i < AMOUNT_PER_PAGE; i++)
        {
            int index = i;
            GUI_OnlineLeaderboardPosition instance = Instance.leaderboard_tab_positions[index];
            originalEvents[index] = instance.favoriteButton.onClick;
            instance.favoriteButton.onClick = new UnityEvent();
            instance.favoriteButton.onClick.AddListener(() => OnFavoriteButtonClicked(index));
        }
    }

    /// <inheritdoc />
    protected override void OnDisable()
    {
        nodes.Clear();

        if (ZeepkistNetwork.IsConnectedToGame)
            return;

        for (int i = 0; i < AMOUNT_PER_PAGE; i++)
        {
            Instance.leaderboard_tab_positions[i].favoriteButton.onClick = originalEvents[i];
        }
    }

    private void OnFavoriteButtonClicked(int i)
    {
        int j = CurrentPage * AMOUNT_PER_PAGE + i;

        GUI_OnlineLeaderboardPosition instance = Instance.leaderboard_tab_positions[i];
        instance.isFavorite = !instance.isFavorite;

        PersonalBest node = nodes[j];

        if (instance.isFavorite)
        {
            OfflineGhostLoader.AddCustomGhost(node.SteamId, node.SteamName, node.GhostId, node.GhostUrl);
        }
        else
        {
            OfflineGhostLoader.RemoveCustomGhost(node.GhostId);
        }

        instance.RedrawFavoriteImage();
    }

    private async UniTaskVoid LoadRecords()
    {
        if (!IsActive)
            return;

        nodes.Clear();
        PlayerManager.Instance.messenger.Log("[GTR] Loading records", 2f);

        if (!await GetPersonalBests())
            return;

        if (!await GetTotalUsers())
            return;

        await GetLevelPoints();

        Draw();
        UpdatePageNumber();
    }

    private async Task<bool> GetPersonalBests()
    {
        Result<PersonalBestsRoot> result = await SdkWrapper.Instance.GraphQLClient.Post<PersonalBestsRoot>(
            $$"""
              {
                  "query": "{ allPersonalBests(condition: {level: \"{{LevelHash}}\"}) { nodes { recordByRecord { time mediaByRecord { nodes { id ghostUrl } } } userByUser { steamId steamName } } } }"
              }
              """);

        if (result.IsFailed)
        {
            Logger.LogError("Unable to get leaderboard records: " + result);
            return false;
        }

        nodes.Clear();

        foreach (PersonalBestsRoot.Data.AllPersonalBests.Node node in result.Value.data.allPersonalBests.nodes)
        {
            string steamId = node.userByUser.steamId;
            string steamName = node.userByUser.steamName;
            double time = node.recordByRecord.time;
            PersonalBestsRoot.Data.AllPersonalBests.Node.RecordByRecord.MediaByRecord.MediaByRecordNode media =
                node.recordByRecord.mediaByRecord.nodes[0];

            nodes.Add(new PersonalBest(steamId,
                steamName,
                time,
                media.id,
                media.ghostUrl));
        }

        nodes.Sort((lhs, rhs) => lhs.Time.CompareTo(rhs.Time));

        MaxPages = (nodes.Count - 1) / AMOUNT_PER_PAGE;
        return true;
    }

    private async Task GetLevelPoints()
    {
        Result<LevelsGetPointsByLevelResponseDTO> pointsByLevel =
            await SdkWrapper.Instance.LevelApi.GetPointsByLevel(LevelHash);

        if (pointsByLevel.IsFailed)
        {
            if (!pointsByLevel.IsNotFound())
            {
                Logger.LogError("Unable to get points: " + pointsByLevel);
            }

            levelPoints = null;
        }
        else
        {
            levelPoints = pointsByLevel.Value.LevelPoints;
        }
    }

    private async Task<bool> GetTotalUsers()
    {
        Result<AllUsersCountRoot> result = await SdkWrapper.Instance.GraphQLClient.Post<AllUsersCountRoot>(
            $$"""
              {
                  "query": "{ allUsers { totalCount } }"
              }
              """);

        if (result.IsFailed)
        {
            Logger.LogError("Unable to get leaderboard records: " + result);
            return false;
        }

        totalUsers = result.Value.data.allUsers.totalCount;
        return true;
    }

    /// <inheritdoc />
    protected override void OnDraw()
    {
        for (int i = 0; i < Instance.leaderboard_tab_positions.Count; ++i)
        {
            int j = CurrentPage * AMOUNT_PER_PAGE + i;

            if (j >= nodes.Count)
                continue;

            PersonalBest node = nodes[j];

            string name = !string.IsNullOrEmpty(node.SteamName)
                ? node.SteamName
                : node.SteamId;

            GUI_OnlineLeaderboardPosition instance = Instance.leaderboard_tab_positions[i];

            instance.favoriteButton.gameObject.SetActive(!ZeepkistNetwork.IsConnectedToGame);
            instance.isFavorite = OfflineGhostLoader.IsCustomGhostEnabled(node.GhostId);
            instance.RedrawFavoriteImage();
            instance.position.gameObject.SetActive(true);
            instance.position.text = (j + 1).ToString(CultureInfo.InvariantCulture);
            instance.position.color = PlayerManager.Instance.GetColorFromPosition(j + 1);
            instance.player_name.text = $"<link=\"{node.SteamId}\">{name}</link>";
            instance.time.text = node.Time.GetFormattedTime();

            if (levelPoints == null)
                continue;

            // This should probably be done only once, but it's fine for now
            int placementPoints = Math.Max(0, nodes.Count - j);
            double a = 1d / (totalUsers / (double)nodes.Count);
            int b = j + 1;
            double c = j < 8 ? fibbonus[j] : 0;
            double points = placementPoints * (1 + a / b) + c;

            instance.pointsWon.text = $"(+{(int)Math.Round(points)})";
        }
    }

    private record PersonalBest(string SteamId, string SteamName, double Time, int GhostId, string GhostUrl);

    private class PersonalBestsRoot
    {
        public Data data { get; set; }

        public class Data
        {
            public AllPersonalBests allPersonalBests { get; set; }

            public class AllPersonalBests
            {
                public int totalCount { get; set; }
                public List<Node> nodes { get; set; }

                public class Node
                {
                    public RecordByRecord recordByRecord { get; set; }
                    public UserByUser userByUser { get; set; }

                    public class RecordByRecord
                    {
                        public double time { get; set; }

                        public MediaByRecord mediaByRecord { get; set; }

                        public class MediaByRecord
                        {
                            public List<MediaByRecordNode> nodes { get; set; }

                            public class MediaByRecordNode
                            {
                                public int id { get; set; }
                                public string ghostUrl { get; set; }
                            }
                        }
                    }

                    public class UserByUser
                    {
                        public string steamName { get; set; }
                        public string steamId { get; set; }
                    }
                }
            }
        }
    }

    private class AllUsersCountRoot
    {
        public Data data { get; set; }

        public class Data
        {
            public AllUsers allUsers { get; set; }

            public class AllUsers
            {
                public int totalCount { get; set; }
            }
        }
    }
}
