using System;
using HarmonyLib;
using ZeepSDK.Leaderboard.Pages;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

public static class RoundLeaderboardMedals
{
    public static event Action<BaseCoreLeaderboardTab, OnlineTabLeaderboardUI> TabEnabled;
    public static event Action<BaseCoreLeaderboardTab> TabDisabled;
    public static event Action<OnlineTabLeaderboardUI> RoundDrawn;

    [HarmonyPatch(typeof(BaseCoreLeaderboardTab), nameof(BaseCoreLeaderboardTab.Enable))]
    public static class Enable
    {
        private static void Postfix(BaseCoreLeaderboardTab __instance, OnlineTabLeaderboardUI __0) =>
            TabEnabled?.Invoke(__instance, __0);
    }

    [HarmonyPatch(typeof(BaseCoreLeaderboardTab), nameof(BaseCoreLeaderboardTab.Disable))]
    public static class Disable
    {
        // Release row ownership before SDK clears rows and hands them to another tab.
        private static void Prefix(BaseCoreLeaderboardTab __instance) => TabDisabled?.Invoke(__instance);
    }

    [HarmonyPatch(typeof(OnlineTabLeaderboardUI), nameof(OnlineTabLeaderboardUI.DrawTabLeaderboard))]
    public static class Draw
    {
        private static void Postfix(OnlineTabLeaderboardUI __instance) => RoundDrawn?.Invoke(__instance);
    }
}
