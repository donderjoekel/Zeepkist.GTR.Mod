using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

internal static class PhotoModeSpeedDisplay
{
    private static FieldInfo _inputDisplayField;

    public static bool TryGet(out SpeedDisplay speedDisplay)
    {
        SpectatorCameraUI spectatorUi = Object.FindObjectOfType<SpectatorCameraUI>();
        if (spectatorUi == null)
        {
            speedDisplay = null;
            return false;
        }

        _inputDisplayField ??= AccessTools.Field(typeof(SpectatorCameraUI), "inputDisplay");
        speedDisplay = _inputDisplayField?.GetValue(spectatorUi) as SpeedDisplay;
        return speedDisplay != null;
    }
}
