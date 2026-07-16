using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

public static class BaseUI_OverlayVisibility
{
    public static event Action<BaseUI> OverlayOpened;
    public static event Action<BaseUI> OverlayClosed;

    private static bool ShouldTrack(BaseUI ui) =>
        ui is PauseMenuUI or SettingsUI or OnlineTabLeaderboardUI;

    [HarmonyPatch(typeof(BaseUI), nameof(BaseUI.Open))]
    public class Open
    {
        private static void Postfix(BaseUI __instance)
        {
            if (!ShouldTrack(__instance))
                return;

            OverlayOpened?.Invoke(__instance);
        }
    }

    [HarmonyPatch(typeof(BaseUI), nameof(BaseUI.Close))]
    public class Close
    {
        private static void Postfix(BaseUI __instance)
        {
            if (!ShouldTrack(__instance))
                return;

            OverlayClosed?.Invoke(__instance);
        }
    }
}
