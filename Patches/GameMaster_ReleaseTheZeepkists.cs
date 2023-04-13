using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(GameMaster), nameof(GameMaster.ReleaseTheZeepkists))]
public class GameMaster_ReleaseTheZeepkists
{
    public static event Action ReleaseTheZeepkists;

    private static void Postfix()
    {
        ReleaseTheZeepkists?.Invoke();
    }
}
