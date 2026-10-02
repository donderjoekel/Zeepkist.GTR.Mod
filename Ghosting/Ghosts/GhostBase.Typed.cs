using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

/// <summary>Constrained struct access avoids boxing frames during playback.</summary>
public abstract class GhostBase<TFrame> : GhostBase where TFrame : struct, IFrame
{
    protected abstract TFrame GetFrame(int index);

    protected override float GetFrameTime(int index) => GetFrame(index).Time;

    protected override void ApplyFrameTransform(int currentIndex, int nextIndex, float interpolation)
    {
        TFrame current = GetFrame(currentIndex);
        TFrame next = GetFrame(nextIndex);
        Ghost.GameObject.transform.SetPositionAndRotation(
            Vector3.Lerp(current.Position, next.Position, interpolation),
            Quaternion.Slerp(current.Rotation, next.Rotation, interpolation));
    }

    protected override void OnSample(int currentIndex, int nextIndex, float interpolation) =>
        OnSample(GetFrame(currentIndex), GetFrame(nextIndex), interpolation);

    protected virtual void OnSample(TFrame current, TFrame next, float interpolation)
    {
    }
}
