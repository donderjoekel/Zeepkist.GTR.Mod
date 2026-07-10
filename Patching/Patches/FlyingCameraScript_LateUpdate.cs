using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(FlyingCameraScript), nameof(FlyingCameraScript.LateUpdate))]
public static class FlyingCameraScript_LateUpdate
{
    private static ConfigService _configService;

    private static ConfigService ConfigService =>
        _configService ??= ServiceHelper.Instance.GetRequiredService<ConfigService>();

    private static bool Prefix(FlyingCameraScript __instance)
    {
        if (__instance.GameMaster == null || !__instance.GameMaster.isPhotoMode)
            return true;

        PhotoModeFlyingCamera.SetCurrent(__instance);

        var freezeKey = ConfigService.PhotoModeCameraFreezeKey.Value;
        if (freezeKey == KeyCode.None || !Input.GetKey(freezeKey))
            return true;

        return false;
    }
}
