using System;
using System.Threading;
using TNRD.Zeepkist.GTR.Patching.Patches;
using UnityEngine;
using ZeepSDK.External.Cysharp.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Screenshots;

public class ScreenshotService
{
    public async UniTask<string> TakeScreenshot(CancellationToken ct)
    {
        bool resultScreenOpen = PlayerManager.Instance.currentMaster.RaceResultsUI.IsOpen;

        if (!resultScreenOpen)
        {
            GameMaster_OpenResultScreen.OpenResultScreen += () => { resultScreenOpen = true; };
        }

        while (!resultScreenOpen && !ct.IsCancellationRequested)
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        if (ct.IsCancellationRequested)
            return null;

        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        return Convert.ToBase64String(screenshot.EncodeToJPG(25));
    }
}
