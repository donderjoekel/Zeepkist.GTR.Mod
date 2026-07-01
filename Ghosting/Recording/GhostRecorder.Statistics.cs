using System;
using System.Collections.Generic;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public partial class GhostRecorder
{
    private const float SpeedCapKmh = 500f;
    private const float TurnDeadzone = 0.1f;

    private static GhostStatistics CalculateStatistics(IReadOnlyList<Frame> frames)
    {
        GhostStatistics stats = new();
        stats.FrameCount = frames.Count;

        if (frames.Count == 0)
            return stats;

        stats.Duration = SafeNonNegative(frames[frames.Count - 1].Time);

        float speedWeighted = 0;
        float speedTime = 0;

        bool wasArmsUp = false;
        bool wasBraking = false;
        bool wasHorn = false;
        bool wasTurningLeft = false;
        bool wasTurningRight = false;

        for (int i = 0; i < frames.Count; i++)
        {
            Frame frame = frames[i];
            if (!IsFinite(frame.Time) || !IsFinite(frame.Speed) || !IsFinite(frame.Position))
                continue;

            stats.TopSpeed = Math.Max(stats.TopSpeed, Math.Min(frame.Speed, SpeedCapKmh));

            bool turningLeft = frame.Steering < -TurnDeadzone;
            bool turningRight = frame.Steering > TurnDeadzone;

            if (frame.ArmsUp && !wasArmsUp)
                stats.ArmsUpCount++;
            if (frame.Braking && !wasBraking)
                stats.BrakeCount++;
            if (frame.Horn && !wasHorn)
                stats.HornCount++;
            if (turningLeft && !wasTurningLeft)
                stats.TurnLeftCount++;
            if (turningRight && !wasTurningRight)
                stats.TurnRightCount++;

            wasArmsUp = frame.ArmsUp;
            wasBraking = frame.Braking;
            wasHorn = frame.Horn;
            wasTurningLeft = turningLeft;
            wasTurningRight = turningRight;

            if (i == 0)
                continue;

            Frame previous = frames[i - 1];
            float dt = frame.Time - previous.Time;

            if (!IsFinite(dt) || dt <= 0 || !IsFinite(previous.Position))
                continue;

            float segmentDistance = Vector3.Distance(previous.Position, frame.Position);
            float impliedSpeed = segmentDistance / dt * 3.6f;

            bool validSegment = IsFinite(segmentDistance) && IsFinite(impliedSpeed) && impliedSpeed <= SpeedCapKmh;

            if (validSegment)
            {
                stats.DistanceTravelled += segmentDistance;
                AddSurfaceDistance(stats.SurfaceDistance, GetSurfaceKey(previous), segmentDistance);
                if (IsInAir(previous))
                    stats.DistanceInAir += segmentDistance;
                else
                    stats.DistanceOnGround += segmentDistance;
            }

            float previousSpeed = Math.Min(previous.Speed, SpeedCapKmh);

            if (IsFinite(previousSpeed))
            {
                speedWeighted += previousSpeed * dt;
                speedTime += dt;
            }

            if (previous.ArmsUp)
                stats.ArmsUpTime += dt;
            if (previous.Braking)
                stats.BrakeTime += dt;
            if (previous.Horn)
                stats.HornTime += dt;
            if (previous.Steering < -TurnDeadzone)
                stats.TurnLeftTime += dt;
            if (previous.Steering > TurnDeadzone)
                stats.TurnRightTime += dt;
            if (previous.SoapboxState == 1)
                stats.SoapTime += dt;
            if (previous.SoapboxState == 2)
                stats.OffroadTime += dt;
            if (previous.SoapboxState == 3)
                stats.ParagliderTime += dt;
            if (IsInAir(previous))
                stats.TimeInAir += dt;
            else
                stats.TimeOnGround += dt;

            AddSurfaceDistance(stats.SurfaceTime, GetSurfaceKey(previous), dt);
        }

        if (speedTime > 0)
            stats.AverageSpeed = speedWeighted / speedTime;

        return stats;
    }

    private static bool IsInAir(Frame frame)
    {
        return frame.WheelState == WheelState.HasNone;
    }

    private static string GetSurfaceKey(Frame frame)
    {
        if (!string.IsNullOrWhiteSpace(frame.Surface))
            return frame.Surface;

        return "tarmac";
    }

    private static void AddSurfaceDistance(Dictionary<string, float> surfaceDistance, string key, float value)
    {
        if (surfaceDistance.TryGetValue(key, out float current))
            surfaceDistance[key] = current + value;
        else
            surfaceDistance[key] = value;
    }

    private static float SafeNonNegative(float value)
    {
        return IsFinite(value) && value > 0 ? value : 0;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
