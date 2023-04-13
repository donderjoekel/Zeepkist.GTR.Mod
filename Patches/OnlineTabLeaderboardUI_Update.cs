using HarmonyLib;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(OnlineTabLeaderboardUI), nameof(OnlineTabLeaderboardUI.Update))]
public class OnlineTabLeaderboardUI_Update
{
    public static int IndexToDraw;

    private static bool Prefix(OnlineTabLeaderboardUI __instance)
    {
        if (!ZeepkistNetwork.IsConnected || ZeepkistNetwork.CurrentLobby == null)
            return true;

        if (!__instance.SwitchAction.buttonDown)
            return true;

        IndexToDraw++;
        if (IndexToDraw >= 3)
            IndexToDraw = 0;

        __instance.drawChampionShipLeaderboard = IndexToDraw switch
        {
            0 => false,
            1 => true,
            _ => __instance.drawChampionShipLeaderboard
        };

        __instance.ClientRedrawLeaderBoard();
        return false;
    }
}
