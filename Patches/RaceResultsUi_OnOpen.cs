using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(RaceResultsUI),nameof(RaceResultsUI.OnOpen))]
public class RaceResultsUi_OnOpen
{
    public static event Action OnOpen;

    private static void Postfix()
    {
        OnOpen?.Invoke();
    }
}
