using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Utilities;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(FlyingCameraScript), "UpdateZeepkistList")]
public static class FlyingCameraScript_UpdateZeepkistList
{
    private static GtrSpectateTargetService _targetService;

    private static GtrSpectateTargetService TargetService =>
        _targetService ??= ServiceHelper.Instance.GetRequiredService<GtrSpectateTargetService>();

    private static bool Prefix(FlyingCameraScript __instance, bool favoritesOnly)
    {
        if (!TargetService.ShouldInjectGhosts)
            return true;

        TargetService.ApplyGhostTargets(__instance);
        return false;
    }
}
