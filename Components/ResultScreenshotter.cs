using System;
using System.Collections;
using TNRD.Zeepkist.GTR.Mod.Patches;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components;

public class ResultScreenshotter : MonoBehaviour
{
    public static event Action<byte[]> ScreenshotTaken;

    private bool hasFinished = false;

    private void Awake()
    {
        GameMaster_ReleaseTheZeepkists.ReleaseTheZeepkists += OnReleaseTheZeepkists;
        GameMaster_CrossedFinishOnline.CrossedFinishOnline += OnCrossedFinishOnline;
        GameMaster_OpenResultScreen.OpenResultScreen += OnOpenResultScreen;
    }

    private void OnOpenResultScreen()
    {
        if (!hasFinished)
            return;

        StartCoroutine(WaitFrameAndSaveScreenshot());
    }

    private IEnumerator WaitFrameAndSaveScreenshot()
    {
        yield return new WaitForEndOfFrame();
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        ScreenshotTaken?.Invoke(screenshot.EncodeToJPG(25));
    }

    private void OnReleaseTheZeepkists()
    {
        hasFinished = false;
    }

    private void OnCrossedFinishOnline()
    {
        hasFinished = true;
    }
}
