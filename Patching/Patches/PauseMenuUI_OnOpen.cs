using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(PauseMenuUI), nameof(PauseMenuUI.OnOpen))]
public class PauseMenuUI_OnOpen
{
    public static event Action Opened;

    private static void Postfix()
    {
        Opened?.Invoke();
    }
}
