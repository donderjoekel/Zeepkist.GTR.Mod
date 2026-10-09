using System;
using HarmonyLib;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(ReadyToReset), nameof(ReadyToReset.HeyYouHitATrigger))]
public static class ReadyToReset_ValidationEvidence
{
    public static event Action<ReadyToReset, GameObject, bool, float, float, float> AcceptedTrigger;
    private readonly struct TriggerState
    {
        public TriggerState(int count, float time) { Count = count; Time = time; }
        public int Count { get; }
        public float Time { get; }
    }

    private static void Prefix(ReadyToReset __instance, out TriggerState __state)
    {
        __state = new TriggerState(__instance.allTriggers.Count, __instance.ticker.GetTicker());
    }

    private static void Postfix(ReadyToReset __instance, GameObject theTrigger, bool isFinish,
        float timeOffset, float velocityKMH, TriggerState __state)
    {
        var captured = __state;
        if (__instance.allTriggers.Count <= captured.Count || !__instance.allTriggers.Contains(theTrigger))
            return;
        if (isFinish && !__instance.actuallyFinished)
            return;
        AcceptedTrigger?.Invoke(__instance, theTrigger, isFinish, captured.Time,
            Mathf.Max(0, captured.Time + timeOffset), velocityKMH);
    }
}
