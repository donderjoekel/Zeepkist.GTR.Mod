using HarmonyLib;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(FlyingCameraScript), nameof(FlyingCameraScript.LateUpdate))]
public static class FlyingCameraScript_LateUpdate
{
    private static bool Prefix(FlyingCameraScript __instance)
    {
        if (__instance.GameMaster == null || !__instance.GameMaster.isPhotoMode)
            return true;

        if (!Input.GetKey(KeyCode.CapsLock))
            return true;

        return false;
    }
}
