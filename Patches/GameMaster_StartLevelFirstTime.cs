using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(GameMaster),nameof(GameMaster.StartLevelFirstTime))]
public class GameMaster_StartLevelFirstTime
{
    public static event Action StartLevelFirstTime;

    private static void Postfix()
    {
        StartLevelFirstTime?.Invoke();
    }
}
