using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(PhotonZeepkist), nameof(PhotonZeepkist.DoShowBuffer))]
public class PhotonZeepkist_DoShowBuffer
{
    public static event Action DoShowBuffer;
    
    private static void Postfix()
    {
        DoShowBuffer?.Invoke();
    }
}
