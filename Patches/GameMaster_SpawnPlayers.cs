using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(GameMaster), nameof(GameMaster.SpawnPlayers))]
public class GameMaster_SpawnPlayers
{
    public static event Action SpawnPlayers;

    private static void Postfix()
    {
        SpawnPlayers?.Invoke();
    }
}
