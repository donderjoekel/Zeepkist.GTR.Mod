using System;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.Mod.Patches;
using ZeepkistClient;
using ZeepSDK.Multiplayer;

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
        if (!await TryCreateLevel())
            return;

        await SubmitForScanIfNecessary();
    }

    private static async UniTask<bool> TryCreateLevel()
    {
        try
        {
            var result = InternalLevelApi.Create();
            if (result.IsSuccess)
                return true;

            Logger.LogError(result.ToString());

            await UniTask.Delay(TimeSpan.FromSeconds(1));
            result = InternalLevelApi.Create();
            if (result.IsSuccess)
                return true;

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

        return false;
    }

    private static async UniTask SubmitForScanIfNecessary()
    {
        try
        {
            var hash = InternalLevelApi.CurrentLevelHash;
            var getResult = await SdkWrapper.Instance.ZworpClient.Get<object>("/levels/hash/" + hash);

            if (getResult.IsSuccess)
                return;

            var levelScriptableObject = MultiplayerApi.GetCurrentLevel();
            var uid = levelScriptableObject.UID;
            var workshopID = levelScriptableObject.WorkshopID;

            ScanRequestModel scanRequestModel = new(workshopID.ToString(), uid, hash);
            var postResult = await SdkWrapper.Instance.ZworpClient.Post("/requests", scanRequestModel);

            if (postResult.IsFailed) Logger.LogError("Failed to submit level for scan: " + postResult);
        }
        catch (Exception e)
        {
            Logger.LogError("Exception while attempting to submit level for scan: " + e);
        }
    }

    private record ScanRequestModel(string WorkshopId, string Uid, string Hash);
}