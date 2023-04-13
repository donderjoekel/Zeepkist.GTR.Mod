using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(PhotonZeepkist), nameof(PhotonZeepkist.DoShowWinscreen))]
public class PhotonZeepkist_DoShowWinscreen
{
    public static event Action DoShowWinscreen;

    private static void Postfix()
    {
        DoShowWinscreen?.Invoke();
    }
}
