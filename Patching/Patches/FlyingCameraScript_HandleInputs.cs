using HarmonyLib;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(FlyingCameraScript), nameof(FlyingCameraScript.HandleInputs))]
public static class FlyingCameraScript_HandleInputs
{
    private static void Postfix(FlyingCameraScript __instance)
    {
        if (__instance.GameMaster == null || !__instance.GameMaster.isPhotoMode)
            return;

        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            return;

        __instance.look = Vector2.zero;
        __instance.lookLocal = Vector2.zero;
    }
}
