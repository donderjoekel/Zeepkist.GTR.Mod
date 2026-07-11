using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(PauseMenuUI), nameof(BaseUI.Close))]
public class PauseMenuUI_Close
{
    public static event Action<bool> Closed;

    private static void Postfix(bool announce)
    {
        Closed?.Invoke(announce);
    }
}
