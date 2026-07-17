namespace TNRD.Zeepkist.GTR.Ghosting;

public static class GhostLimits
{
    public const int MaxCompressedBytes = 16 * 1024 * 1024;
    public const int MaxDecompressedBytes = 96 * 1024 * 1024;
    public const int MaxDecompressionRatio = 64;
    public const int MaxFrames = 250_000;

    public static int GetMaxDecompressedBytes(int compressedBytes)
    {
        if (compressedBytes <= 0 || compressedBytes > MaxCompressedBytes)
            return 0;

        long ratioLimit = (long)compressedBytes * MaxDecompressionRatio;
        return (int)System.Math.Min(MaxDecompressedBytes, ratioLimit);
    }
}
