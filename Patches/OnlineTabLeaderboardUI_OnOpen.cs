using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(OnlineTabLeaderboardUI), nameof(OnlineTabLeaderboardUI.OnOpen))]
internal class OnlineTabLeaderboardUI_OnOpen
{
    public static event Action<OnlineTabLeaderboardUI> OnOpen;
    
    private static bool Prefix(OnlineTabLeaderboardUI __instance)
    {
        OnOpen?.Invoke(__instance);
        return false;
    }
}
