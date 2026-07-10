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
}
