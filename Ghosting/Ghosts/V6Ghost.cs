using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using UnityEngine;
using ZeepkistNetworking;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public partial class V6Ghost : GhostBase<V6Ghost.Frame>
{
    private readonly string _taggedUsername;
    private readonly Color _color;
    private readonly ulong _steamId;
    private readonly CosmeticIDs _cosmeticIds;
    private readonly Frame[] _frames;

    public V6Ghost(
        string taggedUsername,
        Color color,
        ulong steamId,
        CosmeticIDs cosmeticIds,
        Frame[] frames)
    {
        _taggedUsername = taggedUsername;
        _color = color;
        _steamId = steamId;
        _cosmeticIds = cosmeticIds;
        _frames = frames;
    }

    internal override GhostBase CreatePlayback() => new V6Ghost(_taggedUsername, _color, _steamId, _cosmeticIds, _frames);

    protected override int FrameCount => _frames.Length;
    public override Color Color => _color;

    public override void ApplyCosmetics(string steamName)
    {
        CosmeticsV16 cosmetics = new();
        cosmetics.IDsToCosmeticsWithSteamID(_cosmeticIds, _steamId);
        SetupCosmetics(cosmetics, steamName, _steamId);
        if (Ghost.VisualProfile == GhostVisualProfile.Full)
            Ghost.SetCharacterRig(GhostCharacterRig.Create(Ghost.Visuals?.GhostModel));
        ApplySeatedCharacterState(false);
        AlignCharacterRootToSeated();
    }

    protected override float GetFrameTime(int index) => _frames[index].Time;

    protected override Frame GetFrame(int index)
    {
        return _frames[index];
    }

    protected override void OnSample(Frame currentFrame, Frame nextFrame, float interpolation)
    {
        Frame current = currentFrame;
        Frame next = nextFrame;

        if (!current.RagdollState)
        {
            AlignCharacterRootToSeated();
            return;
        }

        Vector3 position = Vector3.Lerp(
            current.RagdollPosition ?? current.Position,
            next.RagdollPosition ?? next.Position,
            interpolation);
        Quaternion rotation = Quaternion.Slerp(
            current.RagdollRotation ?? current.Rotation,
            next.RagdollRotation ?? next.Rotation,
            interpolation);

        AlignRagdollRootToWorld(position, rotation);
    }

    protected override void OnFrameChanged(
        int previousFrameIndex,
        int currentFrameIndex,
        FrameSampleKind sampleKind)
    {
        Frame frame = _frames[currentFrameIndex];
        Frame previousFrame = previousFrameIndex >= 0 ? _frames[previousFrameIndex] : default;
        bool forceState = sampleKind != FrameSampleKind.Advance || previousFrameIndex < 0;
        GhostCharacterPlaybackPose pose = GetCharacterPose(frame);
        if (forceState || GetCharacterPose(previousFrame) != pose)
        {
            if (pose == GhostCharacterPlaybackPose.Ragdoll)
                ApplyRagdollCharacterState();
            else
                ApplySeatedCharacterState(pose == GhostCharacterPlaybackPose.SeatedArmsUp);
        }

        if (!HasFullVisuals)
            return;

        if (forceState)
            SynchronizeHornState(frame.InputFlags.HasFlagFast(InputFlags.Horn));

        if (forceState || GetWheelState(previousFrame.SoapboxFlags) != GetWheelState(frame.SoapboxFlags))
            ApplyWheelState(frame.SoapboxFlags);

        bool paraglider = frame.SoapboxFlags.HasFlagFast(SoapboxFlags.Paraglider);
        if (forceState || previousFrame.SoapboxFlags.HasFlagFast(SoapboxFlags.Paraglider) != paraglider)
            ApplyParagliderState(frame.SoapboxFlags);
    }

    protected override void OnForwardFramesCrossed(int previousFrameIndex, int currentFrameIndex)
    {
        if (!HasFullVisuals)
            return;

        for (int i = previousFrameIndex + 1; i <= currentFrameIndex; i++)
        {
            bool previousHorn = _frames[i - 1].InputFlags.HasFlagFast(InputFlags.Horn);
            bool currentHorn = _frames[i].InputFlags.HasFlagFast(InputFlags.Horn);
            HandleHornEdge(previousHorn, currentHorn);
        }
    }

    protected override void OnStopped(float time)
    {
        ApplySeatedCharacterState(false);
        AlignCharacterRootToSeated();
    }

    private void ApplySeatedCharacterState(bool armsUp)
    {
        Ghost?.CharacterRig?.ApplySeatedPose(armsUp);
        Ghost?.SetCharacterPlaybackState(GhostCharacterPlaybackState.FromSeated(armsUp));
    }

    private static GhostCharacterPlaybackPose GetCharacterPose(Frame frame)
    {
        if (frame.RagdollState)
            return GhostCharacterPlaybackPose.Ragdoll;

        return frame.InputFlags.HasFlagFast(InputFlags.ArmsUp)
            ? GhostCharacterPlaybackPose.SeatedArmsUp
            : GhostCharacterPlaybackPose.Seated;
    }

    private void ApplyRagdollCharacterState()
    {
        Ghost?.CharacterRig?.ApplyStandingRagdollPose();
        Ghost?.SetCharacterPlaybackState(GhostCharacterPlaybackState.FromSegment(true, true, false));
    }

    private void AlignCharacterRootToSeated()
    {
        Ghost?.CharacterRig?.AlignToSeated(Ghost.GameObject.transform);
        Ghost?.SetNameAnchor(Ghost.GameObject.transform);
    }

    private void AlignRagdollRootToWorld(Vector3 position, Quaternion rotation)
    {
        Quaternion visualRotation = Ghost?.CharacterRig != null
            ? Ghost.CharacterRig.GetRagdollWorldRotation(rotation)
            : rotation * (Ghost?.BulkRagdollRotationOffset ?? Quaternion.identity);

        if (Ghost?.CharacterRig != null)
        {
            Ghost.CharacterRig.AlignToWorld(position, visualRotation);
            Ghost.SetNameAnchor(Ghost.CharacterRig.Root.transform);
        }
        else
        {
            Ghost?.SetNameAnchor(Ghost.GameObject.transform);
        }

        if (Ghost?.BulkRagdollCharacterGameObject != null)
            Ghost.BulkRagdollCharacterGameObject.transform.SetPositionAndRotation(position, visualRotation);
    }
}
