using System;
using System.Linq;
using Newtonsoft.Json;
using TNRD.Zeepkist.GTR.Patching.Patches;
using UnityEngine;
using ZeepSDK.Level;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public partial class GhostRecorder
{
    private RunEvidence _evidence;
    private bool _finishCaptured;
    private SphereCollider[] _carTriggerSpheres;
    private SphereCollider[] _ragdollTriggerSpheres;
    private ValidationCaptureMeasurements _validationMeasurements;
    public void EnableValidationMetrics()
    {
        _validationMeasurements = new ValidationCaptureMeasurements { ManagedBytesStart = GC.GetTotalMemory(false) };
    }

    private void StartEvidence()
    {
        _evidence = new RunEvidence
        {
            SphereSamplingVersion = 3,
            RunUuid = Guid.NewGuid().ToString("D"),
            LevelUid = LevelApi.CurrentLevel?.UID ?? "",
            SubmissionLevel = LevelApi.CurrentHashV2?.ZeepHash ?? "",
            CanonicalHash = LevelApi.CurrentHashV2?.Hash ?? "",
            InitialTime = _readyToReset.ticker.what_ticker,
            PhysicsInterval = Time.fixedDeltaTime
        };
        // deadTop is a BoxCollider; topCollider is a different body collider.
        // Checkpoint callbacks accept only owned colliders named TopSphereMan.
        _carTriggerSpheres = _readyToReset.GetComponentsInChildren<SphereCollider>(true);
        _ragdollTriggerSpheres = _readyToReset.character.ragdollTransform
            .GetComponentsInChildren<SphereCollider>(true);
        ReadyToReset_ValidationEvidence.AcceptedTrigger += CaptureTriggerEvidence;
        CaptureFrame(_readyToReset.ticker.what_ticker);
    }

    private SphereSample CaptureSphere(float time, SphereCollider enteringSphere = null)
    {
        if (_validationMeasurements == null) return CaptureSphereCore(time, enteringSphere);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        try { return CaptureSphereCore(time, enteringSphere); }
        finally { _validationMeasurements.AddSample(System.Diagnostics.Stopwatch.GetTimestamp() - started); }
    }

    private SphereSample CaptureSphereCore(float time, SphereCollider enteringSphere)
    {
        var character = _readyToReset?.character;
        if (character == null) return null;
        SphereCollider collider = enteringSphere;
        if (collider != null && (collider.name != "TopSphereMan"
            || collider.GetComponent<ReadyToResetPointer>()?.theScript != _readyToReset)) return null;
        if (collider == null)
        {
            foreach (SphereCollider candidate in character.IsDead() ? _ragdollTriggerSpheres : _carTriggerSpheres)
            {
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy
                    || candidate.name != "TopSphereMan"
                    || candidate.GetComponent<ReadyToResetPointer>()?.theScript != _readyToReset)
                    continue;
                // Multiple active trigger bodies need explicit multi-body evidence support.
                if (collider != null) return null;
                collider = candidate;
            }
        }
        if (collider == null)
            return null;
        UnityEngine.Vector3 center = collider.transform.TransformPoint(collider.center);
        UnityEngine.Vector3 scale = collider.transform.lossyScale;
        return new SphereSample
        {
            Time = time,
            Position = new double[] { center.x, center.y, center.z },
            Radius = collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))
        };
    }

    private void CaptureTriggerEvidence(ReadyToReset player, GameObject trigger, bool finish,
        float rawTime, float adjustedTime, float velocityKmh)
    {
        if (_sealed || player != _readyToReset || _evidence == null || _evidence.Events.Count >= 20001)
            return;
        SphereSample sample = CaptureSphere(rawTime, BlockTrigger_ValidationEvidence.EnteringSphere);
        BlockTriggerFinishOrCheckpoint script = trigger.GetComponent<BlockTriggerFinishOrCheckpoint>();
        if (sample == null || script?.properties == null)
            return;
        // Transform path survives export; root GameObject name can change in level instantiation.
        var path = new System.Collections.Generic.List<string>();
        Transform current = trigger.transform;
        while (current != null && current != script.properties.transform)
        {
            path.Add(current.name);
            current = current.parent;
        }
        path.Reverse();
        _evidence.Events.Add(new TriggerEvidence
        {
            BlockUid = script.properties.UID,
            Shape = string.Join("/", path), Finish = finish,
            RawTime = rawTime, AdjustedTime = adjustedTime, VelocityKmh = velocityKmh, Sample = sample
        });
        if (finish)
        {
            CaptureFrame(rawTime);
            _finishCaptured = true;
        }
    }
}
