using System;
using System.Collections.Generic;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

internal static class GhostInputFrameSampler
{
    internal static bool TrySample<TFrame>(
        IReadOnlyList<TFrame> frames,
        float time,
        Func<TFrame, float> getTime,
        Func<TFrame, float> getSteering,
        Func<TFrame, bool> getArmsUp,
        Func<TFrame, bool> getBraking,
        out GhostInputSample sample,
        Func<TFrame, byte> getZeepkistState = null,
        Func<TFrame, float> getSpeedKmh = null)
    {
        sample = default;
        if (frames == null || frames.Count == 0)
            return false;

        getZeepkistState ??= _ => 0;
        getSpeedKmh ??= _ => 0f;

        TFrame firstFrame = frames[0];
        if (time <= getTime(firstFrame))
        {
            sample = CreateSample(firstFrame, getSteering, getArmsUp, getBraking, getZeepkistState, getSpeedKmh);
            return true;
        }

        TFrame lastFrame = frames[frames.Count - 1];
        if (time >= getTime(lastFrame))
        {
            sample = CreateSample(lastFrame, getSteering, getArmsUp, getBraking, getZeepkistState, getSpeedKmh);
            return true;
        }

        int nextIndex = GhostFrameSearch.FindFirstFrameIndexAtOrAfterTime(frames, time, getTime);
        TFrame previousFrame = frames[nextIndex - 1];
        TFrame nextFrame = frames[nextIndex];
        float t = Mathf.InverseLerp(getTime(previousFrame), getTime(nextFrame), time);
        float steering = Mathf.Lerp(getSteering(previousFrame), getSteering(nextFrame), t);
        float speedKmh = Mathf.Lerp(getSpeedKmh(previousFrame), getSpeedKmh(nextFrame), t);
        sample = new GhostInputSample(
            getArmsUp(previousFrame),
            getBraking(previousFrame),
            steering,
            getZeepkistState(previousFrame),
            speedKmh);
        return true;
    }

    private static GhostInputSample CreateSample<TFrame>(
        TFrame frame,
        Func<TFrame, float> getSteering,
        Func<TFrame, bool> getArmsUp,
        Func<TFrame, bool> getBraking,
        Func<TFrame, byte> getZeepkistState,
        Func<TFrame, float> getSpeedKmh)
    {
        return new GhostInputSample(
            getArmsUp(frame),
            getBraking(frame),
            getSteering(frame),
            getZeepkistState(frame),
            getSpeedKmh(frame));
    }
}
