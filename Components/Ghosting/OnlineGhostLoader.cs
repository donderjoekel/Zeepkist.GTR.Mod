using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.SDK;
using TNRD.Zeepkist.GTR.SDK.Models;
using TNRD.Zeepkist.GTR.SDK.Models.Response;
using UnityEngine;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Mod.Components;

public class OnlineGhostLoader : BaseGhostLoader
{
    private static readonly Dictionary<RecordResponseModel, GameObject> recordToGhost = new();

    public static IReadOnlyDictionary<RecordResponseModel, GameObject> RecordToGhost => recordToGhost;

    /// <inheritdoc />
    protected override bool ContainsGhost(RecordResponseModel recordModel)
    {
        return recordToGhost.ContainsKey(recordModel);
    }

    /// <inheritdoc />
    protected override void AddGhost(RecordResponseModel recordModel, GameObject ghost)
    {
        recordToGhost.Add(recordModel, ghost);
    }

    /// <inheritdoc />
    protected override void ClearGhosts()
    {
        foreach (KeyValuePair<RecordResponseModel, GameObject> kvp in recordToGhost)
        {
            Destroy(kvp.Value);
        }

        recordToGhost.Clear();
    }

    protected override async UniTaskVoid LoadGhosts()
    {
        if (!ZeepkistNetwork.IsConnected)
            return;

        for (int i = 0; i < 1000; i++)
        {
            if (InternalLevelApi.CurrentLevelId != -1)
                break;

            await UniTask.Yield();
        }

        if (InternalLevelApi.CurrentLevelId == -1)
            return;

        Result<RecordsGetResponseDTO> result = await SdkWrapper.Instance.RecordsApi.Get(builder => builder
            .WithLevelId(InternalLevelApi.CurrentLevelId)
            .WithBestOnly(true)
            .WithUserId(SdkWrapper.Instance.UsersApi.UserId));

        RecordResponseModel pb = GetPersonalBestRecordModel(result);

        SpawnPersonalBest(pb);
    }
}
