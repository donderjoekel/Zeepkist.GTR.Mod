using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.UI.Timeline;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(FlyingCameraScript), "HandleInputs")]
public static class FlyingCameraScript_HandleInputs_BlockTimelineTimeScale
{
    private static TimelineModeService _timelineModeService;

    private static TimelineModeService TimelineMode =>
        _timelineModeService ??= ServiceHelper.Instance.GetRequiredService<TimelineModeService>();

    private static void Postfix()
    {
        if (!TimelineMode.IsActive || ZeepkistNetwork.IsConnected)
            return;

        Time.timeScale = 0f;
    }
}
