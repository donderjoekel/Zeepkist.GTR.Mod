using System;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.Mod.Patches;
using UnityEngine;
using ZeepkistClient;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.FluentResults;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Mod.Components;

public class LevelCreator : MonoBehaviourWithLogging
{
    protected override void Awake()
    {
        base.Awake();
        GameMaster_StartLevelFirstTime.StartLevelFirstTime += OnStartLevelFirstTime;
    }

    private void OnStartLevelFirstTime()
    {
        if (!ZeepkistNetwork.IsConnected)
            return;

        CreateLevel().Forget();
    }

    private async UniTaskVoid CreateLevel()
    {
        try
        {
            Result result = InternalLevelApi.Create();
            if (result.IsSuccess)
                return;

            Logger.LogError(result.ToString());

            await UniTask.Delay(TimeSpan.FromSeconds(1));
            result = InternalLevelApi.Create();
            if (result.IsSuccess)
                return;

            Logger.LogError(result.ToString());

            await UniTask.Delay(TimeSpan.FromSeconds(5));
            result = InternalLevelApi.Create();
            if (result.IsFailed)
            {
                Logger.LogError(result.ToString());
                PlayerManager.Instance.messenger.LogError(
                    "[GTR] Failed to load level metadata, records disabled for this level",
                    2.5f);
            }
        }
        catch (Exception e)
        {
            Logger.LogError("Exception while attempting to create level: " + e);
        }
    }
}
