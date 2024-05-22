using System;
using System.Collections.Generic;
using System.Globalization;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.SDK.Extensions;
using ZeepSDK.Leaderboard.Pages;

namespace TNRD.Zeepkist.GTR.Mod.Components.Leaderboard.Pages;

internal class GtrLeaderboardTab : BaseMultiplayerLeaderboardTab
{
    private const int AMOUNT_PER_PAGE = 16;
    private const int TOTAL_ITEMS = AMOUNT_PER_PAGE * 4;

    private class ListItem
    {
        public ListItem(RecordResponseModel record, UserResponseModel user)
        {
            Record = record;
            User = user;
        }

        public RecordResponseModel Record { get; private set; }
        public UserResponseModel User { get; private set; }
    }

    private readonly List<ListItem> listItems = new();
    private LevelPointsResponseModel levelPoints;

    /// <inheritdoc />
    protected override string GetLeaderboardTitle()
    {
        return "GTR Records";
    }

    /// <inheritdoc />
    protected override void OnEnable()
    {
        LoadRecords().Forget();
    }

    private async UniTaskVoid LoadRecords()
    {
        if (!IsActive)
            return;

        listItems.Clear();
        PlayerManager.Instance.messenger.Log("[GTR] Loading records", 2f);

        Result<PersonalBestGetLeaderboardResponseDTO> result = await SdkWrapper.Instance.PersonalBestApi.GetLeaderboard(
            builder =>
            {
                builder
                    .WithLevel(InternalLevelApi.CurrentLevelHash)
                    .WithLimit(TOTAL_ITEMS);
            });

        if (result.IsFailed)
        {
            Logger.LogError("Unable to get leaderboard records: " + result);
            return;
        }

        foreach (PersonalBestGetLeaderboardResponseDTO.Item item in result.Value.Items)
        {
            listItems.Add(new ListItem(item.Record, item.User));
        }

        MaxPages = (listItems.Count - 1) / AMOUNT_PER_PAGE;

        Result<LevelsGetPointsByLevelResponseDTO> pointsByLevel =
            await SdkWrapper.Instance.LevelApi.GetPointsByLevel(InternalLevelApi.CurrentLevelHash);

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

        Draw();
        UpdatePageNumber();
    }

    /// <inheritdoc />
    protected override void OnDisable()
    {
        listItems.Clear();
    }

    /// <inheritdoc />
    protected override void OnDraw()
    {
        for (int i = 0; i < Instance.leaderboard_tab_positions.Count; ++i)
        {
            int j = CurrentPage * AMOUNT_PER_PAGE + i;

            if (j >= listItems.Count)
                continue;

            ListItem listItem = listItems[j];

            string name = !string.IsNullOrEmpty(listItem.User.SteamName)
                ? listItem.User.SteamName
                : listItem.User.SteamId;

            Instance.leaderboard_tab_positions[i].position.gameObject.SetActive(true);
            Instance.leaderboard_tab_positions[i].position.text = (j + 1).ToString(CultureInfo.InvariantCulture);
            Instance.leaderboard_tab_positions[i].position.color = PlayerManager.Instance.GetColorFromPosition(j + 1);
            Instance.leaderboard_tab_positions[i].player_name.text = $"<link=\"{listItem.User.SteamId}\">{name}</link>";
            Instance.leaderboard_tab_positions[i].time.text = listItem.Record.Time.GetFormattedTime();

            if (levelPoints == null)
                continue;

            int points = (int)Math.Floor(CalculatePercentageYield(j + 1) * levelPoints.Points! / 100d);
            // Instance.leaderboard_tab_positions[i].pointsCurrent.text = I2.Loc.LocalizationManager
            //     .GetTranslation("Online/Leaderboard/Points")
            //     .Replace("{[POINTS]}", pb.User.Score.ToString());
            Instance.leaderboard_tab_positions[i].pointsWon.text = $"(+{points})";
        }
    }

    private static double CalculatePercentageYield(int position)
    {
        switch (position)
        {
            case 1:
                return 100;
            case >= 25:
                return 5;
            default:
            {
                double percentage = Math.Round(100 * Math.Exp(-0.15 * (position - 1)));
                return Math.Max(percentage, 5);
            }
        }
    }
}
