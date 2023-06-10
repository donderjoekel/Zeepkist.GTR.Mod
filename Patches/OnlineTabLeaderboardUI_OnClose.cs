using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(OnlineTabLeaderboardUI), nameof(OnlineTabLeaderboardUI.OnClose))]
internal class OnlineTabLeaderboardUI_OnClose
{
    public static event Action<OnlineTabLeaderboardUI> OnClose;

    private static bool Prefix(OnlineTabLeaderboardUI __instance)
    {
        OnClose?.Invoke(__instance);
        return false;
    }
}
