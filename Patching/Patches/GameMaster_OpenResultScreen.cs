﻿using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(GameMaster), nameof(GameMaster.OpenResultScreen))]
public class GameMaster_OpenResultScreen
{
    public static event Action OpenResultScreen;

    private static void Postfix()
    {
        OpenResultScreen?.Invoke();
    }
}
