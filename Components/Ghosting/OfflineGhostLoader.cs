using System.Collections.Generic;
using BepInEx.Logging;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.SDK.Extensions;
using UnityEngine;
using ZeepkistClient;
using ZeepSDK.Level;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting;

public class OfflineGhostLoader : BaseGhostLoader
{
    private static ManualLogSource logger = LoggerFactory.GetLogger<OfflineGhostLoader>();

    private static readonly Dictionary<MediaResponseModel, GameObject> recordToGhost = new();
    private static readonly Dictionary<int, CustomGhost> idToCustomGhost = new();

    private record CustomGhost(string SteamId, string SteamName, int GhostId, string GhostUrl);

    public static void AddCustomGhost(string steamId, string steamName, int ghostId, string ghostUrl)
    {
        logger.LogInfo("Adding custom ghost: " + ghostId);
        idToCustomGhost.Add(ghostId, new CustomGhost(steamId, steamName, ghostId, ghostUrl));
    }

    public static void RemoveCustomGhost(int ghostId)
    {
        logger.LogInfo("Removing custom ghost: " + ghostId);
        idToCustomGhost.Remove(ghostId);
    }

    public static void ClearCustomGhosts()
    {
        logger.LogInfo("Clearing custom ghosts");
        idToCustomGhost.Clear();
    }

    public static bool IsCustomGhostEnabled(int ghostId)
    {
        return idToCustomGhost.ContainsKey(ghostId);
    }

    /// <inheritdoc />
    protected override bool ContainsGhost(MediaResponseModel recordModel)
    {
        return recordToGhost.ContainsKey(recordModel);
    }

    /// <inheritdoc />
    protected override void AddGhost(MediaResponseModel recordModel, GameObject ghost)
    {
        recordToGhost.Add(recordModel, ghost);
    }

    /// <inheritdoc />
    protected override void ClearGhosts()
    {
        foreach (KeyValuePair<MediaResponseModel, GameObject> kvp in recordToGhost)
        {
            Destroy(kvp.Value);
        }

        recordToGhost.Clear();
    }

    protected override async UniTaskVoid LoadGhosts(int identifier)
    {
        if (ZeepkistNetwork.IsConnected)
            return;

        string levelHash = LevelApi.GetLevelHash(PlayerManager.Instance.currentMaster.GlobalLevel);

        foreach (KeyValuePair<int, CustomGhost> kvp in idToCustomGhost)
        {
            logger.LogInfo("Spawning custom ghost: " + kvp.Key);
            SpawnGhost(identifier, kvp.Key, kvp.Value.GhostUrl, kvp.Value.SteamName, kvp.Value.SteamId, null);
        }

        await UniTask.WhenAll(SpawnWorldRecordGhost(identifier, levelHash),
            SpawnPersonalBestGhost(identifier, levelHash));
    }

    private async UniTask SpawnWorldRecordGhost(int identifier, string levelHash)
    {
        if (!Plugin.ConfigShowOfflineWorldRecord.Value)
            return;

        Result<WorldRecordGetGhostResponseDTO> result = await SdkWrapper.Instance.WorldRecordApi.GetGhost(builder =>
        {
            builder
                .WithLevel(levelHash);
        });

        if (result.IsFailed)
        {
            if (!result.IsNotFound())
            {
                Logger.LogError("Unable to load world record: " + result);
            }

            return;
        }

        SpawnWorldRecord(identifier, result.Value.Media, result.Value.Record, result.Value.User);
    }

    private async UniTask SpawnPersonalBestGhost(int identifier, string levelHash)
    {
        if (!Plugin.ConfigShowOfflinePersonalBest.Value)
            return;

        Result<PersonalBestGetGhostResponseDTO> result = await SdkWrapper.Instance.PersonalBestApi.GetGhost(builder =>
        {
            builder
                .WithLevel(levelHash)
                .WithUser(SdkWrapper.Instance.UsersApi.UserId);
        });

        if (result.IsFailed)
        {
            if (!result.IsNotFound())
            {
                Logger.LogError("Unable to load personal best: " + result);
            }

            return;
        }

        SpawnPersonalBest(identifier, result.Value.Media, result.Value.Record, result.Value.User);
    }
}
