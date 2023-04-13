using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(PauseHandler), nameof(PauseHandler.Awake))]
public class PauseHandler_Awake
{
    public static event Action<PauseHandler> Awake;
    
    private static void Postfix(PauseHandler __instance)
    {
        Awake?.Invoke(__instance);
    }
}
