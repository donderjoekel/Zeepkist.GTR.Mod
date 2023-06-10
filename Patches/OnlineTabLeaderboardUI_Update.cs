using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(OnlineTabLeaderboardUI), nameof(OnlineTabLeaderboardUI.Update))]
internal class OnlineTabLeaderboardUI_Update
{
    public static event Action<OnlineTabLeaderboardUI> Update;

    private static bool Prefix(OnlineTabLeaderboardUI __instance)
    {
        Update?.Invoke(__instance);
        return false;
    }
}
