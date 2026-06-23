using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

internal static class PhotoModeFlyingCamera
{
    private static MethodInfo _getCameraTransformMethod;
    private static FieldInfo _cameraTransformField;

    public static FlyingCameraScript Current { get; private set; }

    public static void SetCurrent(FlyingCameraScript flyingCamera)
    {
        Current = flyingCamera;
    }

    public static bool TryGetCameraTransform(out Transform cameraTransform)
    {
        cameraTransform = null;
        if (Current == null)
            return false;

        _getCameraTransformMethod ??=
            AccessTools.Method(typeof(FlyingCameraScript), "GetCameraTransform");
        if (_getCameraTransformMethod != null)
        {
            cameraTransform = (Transform)_getCameraTransformMethod.Invoke(Current, null);
            if (cameraTransform != null)
                return true;
        }

        _cameraTransformField ??=
            AccessTools.Field(typeof(FlyingCameraScript), "cameraTransform");
        if (_cameraTransformField != null)
        {
            cameraTransform = (Transform)_cameraTransformField.GetValue(Current);
            if (cameraTransform != null)
                return true;
        }

        cameraTransform = Current.transform;
        return cameraTransform != null;
    }
}
