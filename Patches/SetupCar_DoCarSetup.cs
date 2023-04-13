using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(SetupCar), nameof(SetupCar.DoCarSetup))]
public class SetupCar_DoCarSetup
{
    public static event Action DoCarSetup;

    private static void Postfix()
    {
        DoCarSetup?.Invoke();
    }
}
