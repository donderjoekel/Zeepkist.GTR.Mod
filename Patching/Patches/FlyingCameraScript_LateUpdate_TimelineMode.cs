using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.UI;
using TNRD.Zeepkist.GTR.UI.Timeline;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(FlyingCameraScript), nameof(FlyingCameraScript.LateUpdate))]
public static class FlyingCameraScript_LateUpdate_TimelineMode
{
    private const float EditorLookFrameFactor = 0.01666666666f;

    private static readonly FieldInfo HelperField =
        AccessTools.Field(typeof(FlyingCameraScript), "helper");

    private static readonly FieldInfo CameraXField =
        AccessTools.Field(typeof(FlyingCameraScript), "cameraX");

    private static readonly Dictionary<FlyingCameraScript, TimelineLookState> SavedLookStates = new();

    private static TimelineModeService _timelineModeService;
    private static PlaybackUiInputState _playbackUiInputState;

    private static TimelineModeService TimelineMode =>
        _timelineModeService ??= ServiceHelper.Instance.GetRequiredService<TimelineModeService>();

    private static PlaybackUiInputState PlaybackUiInput =>
        _playbackUiInputState ??= ServiceHelper.Instance.GetRequiredService<PlaybackUiInputState>();

    private static void Prefix(FlyingCameraScript __instance)
    {
        if (!ShouldApplyTimelineLook(__instance))
            return;

        SavedLookStates[__instance] = new TimelineLookState
        {
            Rotation = __instance.transform.rotation,
            HelperLocalEuler = GetHelperLocalEuler(__instance)
        };
    }

    private static void Postfix(FlyingCameraScript __instance)
    {
        if (!ShouldApplyTimelineLook(__instance))
            return;

        if (!SavedLookStates.TryGetValue(__instance, out TimelineLookState saved))
            return;

        SavedLookStates.Remove(__instance);

        if (PlaybackUiInput.IsPointerOverGtrWindow || !Input.GetMouseButton(1))
        {
            RestoreSavedLook(__instance, saved);
            return;
        }

        ApplyEditorLook(__instance, saved);
    }

    private static bool ShouldApplyTimelineLook(FlyingCameraScript camera)
    {
        if (camera.GameMaster == null || !camera.GameMaster.isPhotoMode)
            return false;

        if (!TimelineMode.IsActive)
            return false;

        return camera.currentCameraState < 3;
    }

    private static void RestoreSavedLook(FlyingCameraScript camera, TimelineLookState saved)
    {
        if (camera.currentCameraState == 2)
        {
            if (HelperField?.GetValue(camera) is GameObject helper)
            {
                helper.transform.localEulerAngles = saved.HelperLocalEuler;
                camera.transform.rotation = helper.transform.rotation;
            }
            else
            {
                camera.transform.rotation = saved.Rotation;
            }

            return;
        }

        camera.transform.rotation = saved.Rotation;
        SyncCameraXFromRotation(camera);
    }

    private static void ApplyEditorLook(FlyingCameraScript camera, TimelineLookState saved)
    {
        float sensitivity = PlayerManager.Instance.instellingen.Settings.editor_sensitivity * EditorLookFrameFactor;
        float yawDelta = Input.GetAxis("Mouse X") * sensitivity;
        float pitchDelta = Input.GetAxis("Mouse Y") * sensitivity;

        if (camera.currentCameraState == 2)
        {
            if (HelperField?.GetValue(camera) is not GameObject helper)
                return;

            helper.transform.localEulerAngles = saved.HelperLocalEuler + new Vector3(-pitchDelta, yawDelta, 0f);
            camera.transform.rotation = helper.transform.rotation;
            return;
        }

        var euler = saved.Rotation.eulerAngles;
        float pitch = NormalizePitch(euler.x) - pitchDelta;
        float yaw = euler.y + yawDelta;
        pitch = Mathf.Clamp(pitch, -90f, 90f);

        camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        CameraXField?.SetValue(camera, pitch);
    }

    private static Vector3 GetHelperLocalEuler(FlyingCameraScript camera)
    {
        if (HelperField?.GetValue(camera) is GameObject helper)
            return helper.transform.localEulerAngles;

        return Vector3.zero;
    }

    private static void SyncCameraXFromRotation(FlyingCameraScript camera)
    {
        float pitch = NormalizePitch(camera.transform.eulerAngles.x);
        CameraXField?.SetValue(camera, pitch);
    }

    private static float NormalizePitch(float pitch)
    {
        if (pitch > 180f)
            pitch -= 360f;

        return pitch;
    }

    private struct TimelineLookState
    {
        public Quaternion Rotation;
        public Vector3 HelperLocalEuler;
    }
}
