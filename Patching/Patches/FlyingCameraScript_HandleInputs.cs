using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(FlyingCameraScript), "HandleInputs")]
public static class FlyingCameraScript_HandleInputs
{
    private static readonly MethodInfo LoopListIteratorMethod =
        AccessTools.Method(typeof(FlyingCameraScript), "LoopListIterator");

    private static readonly FieldInfo Camera6ActionField =
        AccessTools.Field(typeof(FlyingCameraScript), "Camera6Action");

    private static readonly FieldInfo Camera7ActionField =
        AccessTools.Field(typeof(FlyingCameraScript), "Camera7Action");

    private static readonly FieldInfo Camera8ActionField =
        AccessTools.Field(typeof(FlyingCameraScript), "Camera8Action");

    private static readonly FieldInfo NikonField =
        AccessTools.Field(typeof(FlyingCameraScript), "nikon");

    private static GtrSpectateTargetService _targetService;

    private static GtrSpectateTargetService TargetService =>
        _targetService ??= ServiceHelper.Instance.GetRequiredService<GtrSpectateTargetService>();

    public static bool ShouldInjectGhosts => TargetService.ShouldInjectGhosts;

    public static int GetOfflineCameraCycleLimit()
    {
        return ShouldInjectGhosts ? 8 : 5;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for (var i = 0; i < codes.Count - 1; i++)
        {
            if (codes[i].opcode == OpCodes.Ldc_I4_5 &&
                codes[i + 1].Calls(LoopListIteratorMethod))
            {
                codes[i] = new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(FlyingCameraScript_HandleInputs), nameof(GetOfflineCameraCycleLimit)));
            }
        }

        return codes;
    }

    private static void Postfix(FlyingCameraScript __instance)
    {
        if (ZeepkistNetwork.IsConnected || !ShouldInjectGhosts)
            return;

        if (GetButtonDown(Camera6ActionField, __instance))
            ApplySwaybarCameraKey(__instance, targetState: 5, togglesLookBack: true);
        else if (GetButtonDown(Camera7ActionField, __instance))
            ApplySwaybarCameraKey(__instance, targetState: 6, togglesLookBack: true);
        else if (GetButtonDown(Camera8ActionField, __instance))
            ApplySwaybarCameraKey(__instance, targetState: 7, togglesLookBack: false);
    }

    private static bool GetButtonDown(FieldInfo field, FlyingCameraScript camera)
    {
        return field?.GetValue(camera) is InputActionScriptableObject action && action.buttonDown;
    }

    private static void ApplySwaybarCameraKey(
        FlyingCameraScript camera,
        int targetState,
        bool togglesLookBack)
    {
        if (NikonField?.GetValue(camera) is Camera nikon &&
            camera.currentCameraState != 6 &&
            camera.currentCameraState != 5 &&
            camera.currentCameraState != 7)
        {
            nikon.fieldOfView = 90;
        }

        if (togglesLookBack && camera.currentCameraState == targetState)
            camera.alternateCameraState = !camera.alternateCameraState;
        else
            camera.alternateCameraState = false;

        camera.currentCameraState = targetState;
    }
}
