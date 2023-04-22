using System;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.SDK;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(OnlineTabLeaderboardUI), nameof(OnlineTabLeaderboardUI.DrawTabLeaderboard))]
public class OnlineTabLeaderboardUI_DrawTabLeaderboard
{
    private static bool Prefix(OnlineTabLeaderboardUI __instance)
    {
        if (OnlineTabLeaderboardUI_Update.IndexToDraw != 2)
            return true;

        DrawCustom(__instance, true);
        return false;
    }

    private static bool isRefreshing;
    private static RecordsGetResponseDTO responseDTO;

    public static void Clear()
    {
        responseDTO = null;
    }

    private static void RefreshRecordsIfNecessary(OnlineTabLeaderboardUI instance)
    {
        if (isRefreshing)
            return;

        RefreshRecords(instance).Forget();
    }

    private static async UniTask RefreshRecords(OnlineTabLeaderboardUI instance)
    {
        try
        {
            isRefreshing = true;

            PlayerManager.Instance.messenger.Log("[GTR] Loading records", 2f);

            for (int i = 0; i < 1000; i++)
            {
                if (InternalLevelApi.CurrentLevelId != -1)
                    break;

                await UniTask.Yield();
            }

            if (InternalLevelApi.CurrentLevelId == -1)
                return;

            Result<RecordsGetResponseDTO> result = await RecordsApi.Get(builder =>
            {
                builder
                    .WithLevelId(InternalLevelApi.CurrentLevelId)
                    .WithLimit(16)
                    .WithBestOnly(true);
            });

            if (result.IsSuccess)
            {
                responseDTO = result.Value;
                responseDTO.Records = responseDTO.Records.OrderBy(x => x.Time).ToList();
                instance.maxPages = responseDTO.Records.Count - 1 / 16;
            }
            else
            {
                PlayerManager.Instance.messenger.LogError("[GTR] Loading records failed", 2f);
                Plugin.Log.LogError(result.ToString());
            }

            if (OnlineTabLeaderboardUI_Update.IndexToDraw == 2)
            {
                DrawCustom(instance, false);
            }

            isRefreshing = false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError(e);
            throw;
        }
    }

    private static void DrawCustom(OnlineTabLeaderboardUI instance, bool refresh)
    {
        if (refresh)
        {
            RefreshRecordsIfNecessary(instance);
        }

        instance.playersLeaderboard.text = I2.Loc.LocalizationManager.GetTranslation("Online/Leaderboard/PlayerCount");
        instance.playersLeaderboard.text = instance.playersLeaderboard.text
            .Replace("{[PLAYERS]}", ZeepkistNetwork.PlayerList.Count.ToString()).Replace("{[MAXPLAYERS]}",
                ZeepkistNetwork.CurrentLobby.MaxPlayerCount.ToString());

        instance.Page.text = I2.Loc.LocalizationManager.GetTranslation("Online/Lobby/Page");
        instance.Page.text = instance.Page.text.Replace("{[PAGE]}", (instance.currentPage + 1).ToString());

        instance.leaderboardTitle.text = "GTR Records";

        if (responseDTO == null)
            return;

        if (responseDTO.Records.Count == 0)
            return;

        for (int i = 0; i < instance.leaderboard_tab_positions.Count; ++i)
        {
            int j = instance.currentPage * 16 + i;

            if (j >= responseDTO.Records.Count)
                continue;

            RecordResponseModel record = responseDTO.Records[j];

            string name = !string.IsNullOrEmpty(record.User.SteamName)
                ? record.User.SteamName
                : record.User.SteamId;

            instance.leaderboard_tab_positions[i].position.gameObject.SetActive(true);
            instance.leaderboard_tab_positions[i].position.text = (j + 1).ToString(CultureInfo.InvariantCulture);
            instance.leaderboard_tab_positions[i].position.color = PlayerManager.Instance.GetColorFromPosition(j + 1);
            instance.leaderboard_tab_positions[i].player_name.text = $"<link=\"{record.User.SteamId}\">{name}</link>";
            instance.leaderboard_tab_positions[i].time.text = record.Time.Value.GetFormattedTime();
        }
    }
}
