using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.UI;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

internal static class FlyingCameraSpeedZoomBlockState
{
    private static readonly Dictionary<FlyingCameraScript, float> SavedFovs = new();

    public static void SaveFov(FlyingCameraScript camera, float fov)
    {
        SavedFovs[camera] = fov;
    }

    public static bool TryRestoreFov(FlyingCameraScript camera, FieldInfo nikonField)
    {
        if (!SavedFovs.TryGetValue(camera, out float fov))
            return false;

        SavedFovs.Remove(camera);

        if (nikonField?.GetValue(camera) is Camera nikon)
            nikon.fieldOfView = fov;

        return true;
    }
}

[HarmonyPatch(typeof(FlyingCameraScript), "HandleInputs")]
public static class FlyingCameraScript_HandleInputs_BlockSpeedZoom
{
    private static PlaybackUiInputState _playbackUiInputState;

    private static PlaybackUiInputState PlaybackUiInput =>
        _playbackUiInputState ??= ServiceHelper.Instance.GetRequiredService<PlaybackUiInputState>();

    private static void Postfix(FlyingCameraScript __instance)
    {
        if (__instance.GameMaster == null || !__instance.GameMaster.isPhotoMode)
            return;

        if (!PlaybackUiInput.IsPointerOverSpeedControl)
            return;

        if (__instance.currentCameraState == 4)
            return;

        FlyingCameraSpeedZoomBlockState.SaveFov(__instance, __instance.GetFOV());
    }
}

[HarmonyPatch(typeof(FlyingCameraScript), nameof(FlyingCameraScript.LateUpdate))]
public static class FlyingCameraScript_LateUpdate_BlockSpeedZoom
{
    private static readonly FieldInfo NikonField =
        AccessTools.Field(typeof(FlyingCameraScript), "nikon");

    private static void Postfix(FlyingCameraScript __instance)
    {
        FlyingCameraSpeedZoomBlockState.TryRestoreFov(__instance, NikonField);
    }
}
