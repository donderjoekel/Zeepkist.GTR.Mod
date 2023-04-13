using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(OnlineTabLeaderboardUI), nameof(OnlineTabLeaderboardUI.OnOpen))]
public class OnlineTabLeaderboardUI_OnOpen
{
    private static void Prefix()
    {
        OnlineTabLeaderboardUI_DrawTabLeaderboard.Clear();
    }
}
