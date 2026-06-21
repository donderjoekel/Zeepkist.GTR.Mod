namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public static class GhostLoadBudget
{
    public const int MaximumAdditionsPerFrame = 16;
    public const double MaximumMillisecondsPerFrame = 2;

    public static bool CanProcessNext(int processedCount, double elapsedMilliseconds)
    {
        return processedCount < MaximumAdditionsPerFrame &&
               elapsedMilliseconds < MaximumMillisecondsPerFrame;
    }

    public static bool IsCurrentGeneration(int operationGeneration, int currentGeneration)
    {
        return operationGeneration == currentGeneration;
    }
}
