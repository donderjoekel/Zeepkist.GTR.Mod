using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(MainMenuUI), nameof(MainMenuUI.Awake))]
public class MainMenuUi_Awake
{
    public static event Action Postfixed;

    private static void Postfix()
    {
        Postfixed?.Invoke();
    }
}
