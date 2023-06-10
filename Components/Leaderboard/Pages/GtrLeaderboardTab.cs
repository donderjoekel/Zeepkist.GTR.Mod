using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Logging;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components.Leaderboard.Pages;

internal class GtrLeaderboardTab : BaseLeaderboardTab
{
    private readonly List<RecordResponseModel> records = new List<RecordResponseModel>();

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
        PlayerManager.Instance.messenger.Log("[GTR] Loading records", 2f);

        Result<RecordsGetResponseDTO> result = await Sdk.Instance.RecordsApi.Get(builder =>
        {
            builder
                .WithLimit(128)
                .WithLevelId(InternalLevelApi.CurrentLevelId)
                .WithBestOnly(true);
        });

        if (!IsActive)
            return;

        if (result.IsSuccess)
        {
            records.Clear();
            records.AddRange(result.Value.Records.OrderBy(x => x.Time));
            MaxPages = (records.Count - 1) / 16;

            // Force draw
            Draw();
        }
        else
        {
            PlayerManager.Instance.messenger.LogError("[GTR] Loading records failed", 2f);
            Logger.LogError(result.ToString());
        }

        UpdatePageNumber();
    }

    /// <inheritdoc />
    protected override void OnDisable()
    {
        records.Clear();
    }

    /// <inheritdoc />
    protected override void OnDraw()
    {
        for (int i = 0; i < Instance.leaderboard_tab_positions.Count; ++i)
        {
            int j = CurrentPage * 16 + i;

            if (j >= records.Count)
                continue;

            RecordResponseModel record = records[j];

            string name = !string.IsNullOrEmpty(record.User!.SteamName)
                ? record.User.SteamName
                : record.User.SteamId;

            Instance.leaderboard_tab_positions[i].position.gameObject.SetActive(true);
            Instance.leaderboard_tab_positions[i].position.text = (j + 1).ToString(CultureInfo.InvariantCulture);
            Instance.leaderboard_tab_positions[i].position.color = PlayerManager.Instance.GetColorFromPosition(j + 1);
            Instance.leaderboard_tab_positions[i].player_name.text = $"<link=\"{record.User.SteamId}\">{name}</link>";
            Instance.leaderboard_tab_positions[i].time.text = record.Time!.Value.GetFormattedTime();

            Instance.leaderboard_tab_positions[i].pointsCurrent.text = I2.Loc.LocalizationManager
                .GetTranslation("Online/Leaderboard/Points")
                .Replace("{[POINTS]}", record.User.Score.ToString());

            double percentage = CalculatePercentageYield(j + 1);
            int points = (int)Math.Floor(percentage * (double)record.Level!.Points! / 100d);
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
