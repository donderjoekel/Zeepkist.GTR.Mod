using System;
using HarmonyLib;
using UnityEngine;
using TNRD.Zeepkist.GTR.Ghosting.Recording;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(ReadyToReset), nameof(ReadyToReset.HeyYouHitATrigger))]
public static class ReadyToReset_ValidationEvidence
{
    internal static event Action<ReadyToReset, GameObject, bool, float, float, float, SphereSample> AcceptedTrigger;
    internal static event Func<ReadyToReset, float, SphereSample> CaptureSample;
    private readonly struct TriggerState
    {
        public TriggerState(int count, float time, SphereSample sample) { Count = count; Time = time; Sample = sample; }
        public int Count { get; }
        public float Time { get; }
        public SphereSample Sample { get; }
    }

    private static void Prefix(ReadyToReset __instance, out TriggerState __state)
    {
        float time = __instance.ticker.GetTicker();
        __state = new TriggerState(__instance.allTriggers.Count, time, CaptureSample?.Invoke(__instance, time));
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
            Mathf.Max(0, captured.Time + timeOffset), velocityKMH, captured.Sample);
    }
}
