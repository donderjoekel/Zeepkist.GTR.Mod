using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(OnlineChatUI), nameof(OnlineChatUI.Awake))]
public class OnlineChatUI_Awake
{
    public static event Action<OnlineChatUI> Awake;

    private static void Postfix(OnlineChatUI __instance)
    {
        Awake?.Invoke(__instance);
    }
}
