using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(OnlineChatUI), nameof(OnlineChatUI.EnableSmallBox))]
public class OnlineChatUI_EnableSmallBox
{
    public static event Action EnableSmallBox;

    private static void Postfix()
    {
        EnableSmallBox?.Invoke();
    }
}
