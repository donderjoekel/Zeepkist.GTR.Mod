using System;
using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

internal static class GhostFrameSearch
{
    internal static int FindFirstFrameIndexAtOrAfterTime(
        int frameCount,
        float time,
        Func<int, float> getTimeAtIndex)
    {
        int low = 1;
        int high = frameCount - 1;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            if (getTimeAtIndex(mid) < time)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    internal static int FindFirstFrameIndexAtOrAfterTime<TFrame>(
        IReadOnlyList<TFrame> frames,
        float time,
        Func<TFrame, float> getTime)
    {
        return FindFirstFrameIndexAtOrAfterTime(frames.Count, time, index => getTime(frames[index]));
    }

    internal static bool TryGetAdjacentFrameTime(
        int frameCount,
        float currentTime,
        int direction,
        float timeEpsilon,
        Func<int, float> getTimeAtIndex,
        out float adjacentTime)
    {
        adjacentTime = 0f;
        if (frameCount <= 0 || direction is not (1 or -1))
            return false;

        if (direction > 0)
        {
            int nextIndex = FindFirstFrameIndexAtOrAfterTime(
                frameCount,
                currentTime + timeEpsilon,
                getTimeAtIndex);
            if (nextIndex >= frameCount)
                return false;

            adjacentTime = getTimeAtIndex(nextIndex);
            return true;
        }

        int nextOrEqualIndex = FindFirstFrameIndexAtOrAfterTime(frameCount, currentTime, getTimeAtIndex);
        int prevIndex = nextOrEqualIndex - 1;
        if (prevIndex < 0)
            return false;

        if (prevIndex == 0 && currentTime <= getTimeAtIndex(0) + timeEpsilon)
            return false;

        adjacentTime = getTimeAtIndex(prevIndex);
        return true;
    }
}
