using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Utilities;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(FlyingCameraScript), "GetCurrentTargetIndex")]
public static class FlyingCameraScript_GetCurrentTargetIndex
{
    private static GtrSpectateTargetService _targetService;

    private static GtrSpectateTargetService TargetService =>
        _targetService ??= ServiceHelper.Instance.GetRequiredService<GtrSpectateTargetService>();

    private static bool Prefix(FlyingCameraScript __instance, ref int __result)
    {
        if (!TargetService.ShouldInjectGhosts)
            return true;

        __result = TargetService.GetCurrentTargetIndex(__instance);
        return false;
    }
}
