using System;
using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Mod.Api.Records.Models;
using TNRD.Zeepkist.GTR.SDK.Client;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.FluentResults;

namespace TNRD.Zeepkist.GTR.Mod.Api.Records;

public static class InternalRecordsApi
{
    public static async UniTask<Result> Submit(
        int level,
        int user,
        float time,
        List<float> splits,
        string ghostJson,
        byte[] screenshotBuffer,
        bool isValid
    )
    {
        SubmitRecordRequestModel submitRecordRequestModel = new SubmitRecordRequestModel()
        {
            Level = level,
            User = user,
            Time = time,
            Splits = splits,
            GhostData = ghostJson,
            ScreenshotData = Convert.ToBase64String(screenshotBuffer),
            GameVersion = $"{PlayerManager.Instance.version.version}.{PlayerManager.Instance.version.patch}",
            IsValid = isValid
        };

        Result<SubmitRecordResponseModel> result =
            await Sdk.Instance.ApiClient.Post<SubmitRecordResponseModel>("records/submit", submitRecordRequestModel);

        if (result.IsFailed)
            return result.ToResult();

        return Result.Ok();
    }
}
