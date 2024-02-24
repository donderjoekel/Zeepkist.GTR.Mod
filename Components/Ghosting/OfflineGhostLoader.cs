using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.Mod.Patches;
using TNRD.Zeepkist.GTR.SDK.Extensions;
using UnityEngine;
using ZeepkistClient;
using ZeepSDK.Level;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting;

public class OfflineGhostLoader : BaseGhostLoader
{
    private static readonly Dictionary<MediaResponseModel, GameObject> recordToGhost = new();

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
