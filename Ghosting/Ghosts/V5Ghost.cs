using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using UnityEngine;
using ZeepkistNetworking;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public partial class V5Ghost : GhostBase<V5Ghost.Frame>, IGhostInputProvider
{
    private readonly string _taggedUsername;
    private readonly Color _color;
    private readonly ulong _steamId;
    private readonly CosmeticIDs _cosmeticIds;
    private readonly Frame[] _frames;

    public V5Ghost(
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

    internal override GhostBase CreatePlayback() => new V5Ghost(_taggedUsername, _color, _steamId, _cosmeticIds, _frames);

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

    public bool TrySampleInputAtTime(float time, out GhostInputSample sample)
    {
        return GhostInputFrameSampler.TrySample(
            _frames,
            time,
            frame => frame.Time,
            frame => frame.Steering,
            frame => frame.InputFlags.HasFlagFast(InputFlags.ArmsUp),
            frame => frame.InputFlags.HasFlagFast(InputFlags.Braking),
            out sample,
            MapZeepkistState,
            frame => frame.Speed);
    }

    private static byte MapZeepkistState(Frame frame)
    {
        SoapboxFlags flags = frame.SoapboxFlags;
        if (flags.HasFlagFast(SoapboxFlags.Soap))
            return 1;

        return 0;
    }

    protected override void OnSample(Frame currentFrame, Frame nextFrame, float interpolation)
    {
        AlignCharacterRootToSeated();
    }

    protected override void OnFrameChanged(
        int previousFrameIndex,
        int currentFrameIndex,
        FrameSampleKind sampleKind)
    {
        Frame frame = _frames[currentFrameIndex];
        Frame previousFrame = previousFrameIndex >= 0 ? _frames[previousFrameIndex] : default;
        bool forceState = sampleKind != FrameSampleKind.Advance || previousFrameIndex < 0;
        bool armsUp = frame.InputFlags.HasFlagFast(InputFlags.ArmsUp);
        if (forceState || previousFrame.InputFlags.HasFlagFast(InputFlags.ArmsUp) != armsUp)
            ApplySeatedCharacterState(armsUp);

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

    private void AlignCharacterRootToSeated()
    {
        Ghost?.CharacterRig?.AlignToSeated(Ghost.GameObject.transform);
        Ghost?.SetNameAnchor(Ghost.GameObject.transform);
    }
}
