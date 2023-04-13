using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(MainMenuUI), nameof(MainMenuUI.Awake))]
public class MainMenuUi_Awake
{
    public static event Action Awake;

    private static void Postfix()
    {
        Awake?.Invoke();
    }
}
