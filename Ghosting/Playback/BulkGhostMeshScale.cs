using System;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public static class BulkGhostMeshScale
{
    public static float CalculateUniformScale(
        float sourceMaximumSize,
        float bakedMaximumSize,
        float tolerance = 0.05f)
    {
        if (sourceMaximumSize <= 0 || bakedMaximumSize <= 0)
            return 1;

        float scale = sourceMaximumSize / bakedMaximumSize;
        return Math.Abs(scale - 1) <= tolerance ? 1 : scale;
    }
}
