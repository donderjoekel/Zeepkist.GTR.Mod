using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(GameMaster), nameof(GameMaster.CrossedFinishOnline))]
public class GameMaster_CrossedFinishOnline
{
    public static event Action CrossedFinishOnline;

    private static void Postfix()
    {
        CrossedFinishOnline?.Invoke();
    }
}
