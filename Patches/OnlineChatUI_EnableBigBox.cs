using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(OnlineChatUI), nameof(OnlineChatUI.EnableBigBox))]
public class OnlineChatUI_EnableBigBox
{
    public static event Action EnableBigBox;

    private static void Postfix()
    {
        EnableBigBox?.Invoke();
    }
}
