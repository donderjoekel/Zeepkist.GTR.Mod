using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(SetupGame), nameof(SetupGame.FinishLoading))]
public class SetupGame_FinishLoading
{
    public static event Action FinishLoading;
    
    private static void Postfix()
    {
        FinishLoading?.Invoke();
    }
}
