using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(SpectatorCameraUI), nameof(SpectatorCameraUI.OnOpen))]
public static class SpectatorCameraUi_OnOpen
{
    public static event Action Enabled;

    private static void Postfix()
    {
        Enabled?.Invoke();
    }
}

[HarmonyPatch(typeof(SpectatorCameraUI), nameof(SpectatorCameraUI.OnClose))]
public static class SpectatorCameraUi_OnClose
{
    public static event Action Disabled;

    private static void Postfix()
    {
        Disabled?.Invoke();
    }
}
