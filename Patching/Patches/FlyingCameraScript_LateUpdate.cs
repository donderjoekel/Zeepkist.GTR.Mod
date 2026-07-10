using HarmonyLib;
using TNRD.Zeepkist.GTR.Ghosting.Playback;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(FlyingCameraScript), nameof(FlyingCameraScript.LateUpdate))]
public static class FlyingCameraScript_LateUpdate
{
    private static void Postfix(FlyingCameraScript __instance)
    {
        if (__instance.GameMaster == null || !__instance.GameMaster.isPhotoMode)
            return;

        PhotoModeFlyingCamera.SetCurrent(__instance);
    }
}
