using System;
using HarmonyLib;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

[HarmonyPatch(typeof(BlockTriggerFinishOrCheckpoint), "OnTriggerEnter")]
internal static class BlockTrigger_ValidationEvidence
{
    [ThreadStatic] internal static SphereCollider EnteringSphere;

    private static void Prefix(Collider other, out SphereCollider __state)
    {
        __state = EnteringSphere;
        EnteringSphere = other as SphereCollider;
    }

    private static void Finalizer(SphereCollider __state)
    {
        // Restore nested callback context even if game code throws.
        EnteringSphere = __state;
    }
}
