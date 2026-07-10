using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Utilities;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(FlyingCameraScript), "UpdateZeepkistList")]
public static class FlyingCameraScript_UpdateZeepkistList
{
    private static GtrSpectateTargetService _targetService;

    private static GtrSpectateTargetService TargetService =>
        _targetService ??= ServiceHelper.Instance.GetRequiredService<GtrSpectateTargetService>();

    private static void Postfix(FlyingCameraScript __instance, bool favoritesOnly)
    {
        if (ZeepkistNetwork.IsConnected || favoritesOnly)
            return;

        if (!TargetService.ShouldInjectGhosts)
            return;

        TargetService.ApplyGhostTargets(__instance);
    }
}
