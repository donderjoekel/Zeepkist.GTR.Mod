using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

internal static class PhotoModeFlyingCamera
{
    public static FlyingCameraScript Current { get; private set; }

    public static void SetCurrent(FlyingCameraScript flyingCamera)
    {
        Current = flyingCamera;
    }

    public static bool TryGetCameraTransform(out Transform cameraTransform)
    {
        cameraTransform = Current != null ? Current.transform : null;
        return cameraTransform != null;
    }
}
