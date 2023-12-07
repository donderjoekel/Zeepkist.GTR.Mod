using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.SDK.Extensions;
using UnityEngine;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting;

public class OnlineGhostLoader : BaseGhostLoader
{
    private static readonly Dictionary<MediaResponseModel, GameObject> recordToGhost = new();

    /// <inheritdoc />
    protected override bool ContainsGhost(MediaResponseModel mediaModel)
    {
        return recordToGhost.ContainsKey(mediaModel);
    }

    /// <inheritdoc />
    protected override void AddGhost(MediaResponseModel mediaModel, GameObject ghost)
    {
        recordToGhost.Add(mediaModel, ghost);
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
        if (!ZeepkistNetwork.IsConnected)
            return;

        for (int i = 0; i < 1000; i++)
        {
            if (!string.IsNullOrEmpty(InternalLevelApi.CurrentLevelHash))
                break;

            await UniTask.Yield();
        }

        if (string.IsNullOrEmpty(InternalLevelApi.CurrentLevelHash))
            return;

        Result<PersonalBestGetGhostResponseDTO> result = await SdkWrapper.Instance.PersonalBestApi.GetGhost(builder =>
        {
            builder
                .WithLevel(InternalLevelApi.CurrentLevelHash)
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
