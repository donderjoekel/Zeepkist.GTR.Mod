using System.Reflection;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using TNRD.Zeepkist.GTR.Patching.Patches;
using UnityEngine;
using Xunit;
using ZeepSDK.External.Cysharp.Threading.Tasks;
namespace Zeepkist.GTR.Mod.Tests;
public class CaptureEvidenceTests
{
    private static readonly Type Patch = typeof(ReadyToReset_ValidationEvidence);
    private static (ReadyToReset, SphereCollider) Player()
    {
        var player = new ReadyToReset();
        var sphere = new SphereCollider { pointer = new ReadyToResetPointer { theScript = player } };
        player.spheres = [sphere]; return (player, sphere);
    }
    private static object Begin(ReadyToReset player, SphereCollider sphere)
    {
        BlockTrigger_ValidationEvidence.EnteringSphere = sphere;
        object[] args = [player, null]; Patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
        return args[1];
    }
    private static void End(ReadyToReset player, GameObject trigger, bool finish, float offset, object state) =>
        Patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [player, trigger, finish, offset, 36f, state]);
    private static GameObject Trigger()
    {
        var trigger = new GameObject();
        trigger.component = new BlockTriggerFinishOrCheckpoint { properties = new CaptureProperties { transform = trigger.transform } };
        return trigger;
    }
    [Fact]
    public void PhysicsPosePreservesScaledColliderOffsetAndRawTime()
    {
        var (player, sphere) = Player();
        sphere.transform.position = new(100, 0, 0); sphere.transform.lossyScale = new(2, 3, 4); sphere.center = new(1, 0, 0);
        sphere.attachedRigidbody.transform.position = new(100, 0, 0); sphere.attachedRigidbody.position = new(10, 0, 0);
        sphere.attachedRigidbody.rotation = new(0, 0, (float)Math.Sqrt(.5), (float)Math.Sqrt(.5));
        var recorder = new GhostRecorder(player); recorder.StartCapture();
        try
        {
            var sample = recorder.Evidence.Samples[0];
            Assert.Equal(10, sample.Position[0], 5); Assert.Equal(2, sample.Position[1], 5); Assert.Equal(1.08, sample.Radius, 5);
            player.ticker.what_ticker = 1; PlayerLoopHelper.Tick(); Assert.Equal(1, recorder.Evidence.Samples.Last().Time);
            Assert.Equal(1, recorder.CapturedFrames); // Evidence ticks do not add visual frames.
        }
        finally { recorder.StopCapture(); PlayerLoopHelper.Tick(); }
    }
    [Fact]
    public void FinishRetainsPrefixSphereDespiteSideEffectsAndStopsSampling()
    {
        var (player, sphere) = Player(); var recorder = new GhostRecorder(player); recorder.StartCapture();
        try
        {
            player.ticker.what_ticker = 1; var trigger = Trigger(); sphere.attachedRigidbody.position = new(3, 0, 0);
            object state = Begin(player, sphere);
            sphere.attachedRigidbody.position = new(99, 0, 0); player.allTriggers.Add(trigger); player.actuallyFinished = true;
            End(player, trigger, true, .25f, state);
            var finish = Assert.Single(recorder.Evidence.Events);
            Assert.Equal(3, finish.Sample.Position[0]); Assert.Equal(1, finish.RawTime); Assert.Equal(1.25, finish.AdjustedTime);
            Assert.Same(finish.Sample, recorder.Evidence.Samples.Last());
            int count = recorder.Evidence.Samples.Count; PlayerLoopHelper.Tick(); Assert.Equal(count, recorder.Evidence.Samples.Count);
        }
        finally { recorder.StopCapture(); PlayerLoopHelper.Tick(); BlockTrigger_ValidationEvidence.EnteringSphere = null; }
    }
    [Fact]
    public void ExactEnteringRagdollSphereRejectsDuplicatesAndDoesNotAddCheckpointSamples()
    {
        var (player, car) = Player(); var (_, ragdoll) = Player(); ragdoll.pointer.theScript = player;
        player.character.ragdollTransform.spheres = [ragdoll]; player.character.dead = true;
        ragdoll.attachedRigidbody.position = new(5, 0, 0); car.attachedRigidbody.position = new(9, 0, 0);
        var recorder = new GhostRecorder(player); recorder.StartCapture();
        try
        {
            Assert.Equal(5, recorder.Evidence.Samples[0].Position[0]);
            player.ticker.what_ticker = .1f; var trigger = Trigger(); object state = Begin(player, car);
            player.allTriggers.Add(trigger); End(player, trigger, false, -.25f, state);
            var checkpoint = Assert.Single(recorder.Evidence.Events); Assert.Equal(9, checkpoint.Sample.Position[0]);
            Assert.Equal(0, checkpoint.AdjustedTime); Assert.Single(recorder.Evidence.Samples);
            state = Begin(player, car); End(player, trigger, false, 0, state); Assert.Single(recorder.Evidence.Events);
        }
        finally { recorder.StopCapture(); PlayerLoopHelper.Tick(); BlockTrigger_ValidationEvidence.EnteringSphere = null; }
        var next = new GhostRecorder(player); next.StartCapture();
        try { Assert.Empty(next.Evidence.Events); Assert.NotEqual(recorder.Evidence.RunUuid, next.Evidence.RunUuid); }
        finally { next.StopCapture(); PlayerLoopHelper.Tick(); }
    }
    [Fact]
    public void FrameLimitReservesExactTerminalSample()
    {
        var (player, sphere) = Player(); var recorder = new GhostRecorder(player); recorder.StartCapture();
        try
        {
            var first = recorder.Evidence.Samples[0];
            recorder.Evidence.Samples.AddRange(Enumerable.Repeat(first, TNRD.Zeepkist.GTR.Ghosting.GhostLimits.MaxFrames - 2));
            PlayerLoopHelper.Tick(); Assert.Equal(TNRD.Zeepkist.GTR.Ghosting.GhostLimits.MaxFrames - 1, recorder.Evidence.Samples.Count);
            player.ticker.what_ticker = 1; var trigger = Trigger(); var state = Begin(player, sphere);
            player.allTriggers.Add(trigger); player.actuallyFinished = true; End(player, trigger, true, 0, state);
            Assert.Equal(TNRD.Zeepkist.GTR.Ghosting.GhostLimits.MaxFrames, recorder.Evidence.Samples.Count);
            Assert.Same(Assert.Single(recorder.Evidence.Events).Sample, recorder.Evidence.Samples.Last());
        }
        finally { recorder.StopCapture(); PlayerLoopHelper.Tick(); BlockTrigger_ValidationEvidence.EnteringSphere = null; }
    }

}
