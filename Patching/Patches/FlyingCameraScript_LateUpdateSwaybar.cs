using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(FlyingCameraScript), nameof(FlyingCameraScript.LateUpdate))]
public static class FlyingCameraScript_LateUpdateSwaybar
{
    private static GtrSpectateTargetService _targetService;
    private static GtrGhostSpectateRigService _rigService;
    private static ConfigService _configService;

    private static GtrSpectateTargetService TargetService =>
        _targetService ??= ServiceHelper.Instance.GetRequiredService<GtrSpectateTargetService>();

    private static GtrGhostSpectateRigService RigService =>
        _rigService ??= ServiceHelper.Instance.GetRequiredService<GtrGhostSpectateRigService>();

    private static ConfigService ConfigService =>
        _configService ??= ServiceHelper.Instance.GetRequiredService<ConfigService>();

    private static void Postfix(FlyingCameraScript __instance)
    {
        if (__instance.GameMaster == null || !__instance.GameMaster.isPhotoMode)
            return;

        if (!TargetService.ShouldInjectGhosts)
            return;

        var freezeKey = ConfigService.PhotoModeCameraFreezeKey.Value;
        if (freezeKey != KeyCode.None && Input.GetKey(freezeKey))
            return;

        int cameraState = __instance.currentCameraState;
        if (cameraState != 5 && cameraState != 6 && cameraState != 7)
            return;

        Transform targetTransform = __instance.currentTarget?.transform;
        if (targetTransform == null)
            return;

        if (!RigService.TryGetRig(targetTransform, out GtrGhostSpectateRig rig))
            return;

        if (cameraState == 5 || cameraState == 7)
            rig.Swaybar.SwayMode = GhostCameraSwaybar.SwayBarMode.moveStatic;
        else
            rig.Swaybar.SwayMode = GhostCameraSwaybar.SwayBarMode.moveDynamic;

        rig.Swaybar.LookBackwards = __instance.alternateCameraState;

        if (cameraState == 7)
        {
            __instance.transform.SetPositionAndRotation(
                rig.BonnetCameraPosition.position,
                rig.BonnetCameraPosition.rotation);
        }
        else
        {
            __instance.transform.SetPositionAndRotation(
                rig.SwaybarCameraPosition.position,
                rig.SwaybarCameraPosition.rotation);
        }
    }
}
