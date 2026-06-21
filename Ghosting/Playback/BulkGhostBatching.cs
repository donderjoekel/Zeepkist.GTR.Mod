namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public static class BulkGhostBatching
{
    public const int MaximumInstancesPerBatch = 1023;

    public static int GetBatchCount(int instanceCount)
    {
        if (instanceCount <= 0)
            return 0;

        return (instanceCount + MaximumInstancesPerBatch - 1) / MaximumInstancesPerBatch;
    }

    public static int GetDrawCallCount(int instanceCount, int materialCount)
    {
        if (materialCount <= 0)
            return 0;

        return GetBatchCount(instanceCount) * materialCount;
    }
}
