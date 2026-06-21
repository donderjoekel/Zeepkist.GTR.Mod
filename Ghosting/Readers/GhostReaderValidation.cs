using System.IO;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

internal static class GhostReaderValidation
{
    public static int ReadFrameCount(BinaryReader reader)
    {
        int frameCount = reader.ReadInt32();
        if (frameCount < 0 || frameCount > GhostLimits.MaxFrames)
            throw new InvalidDataException($"Invalid ghost frame count: {frameCount}.");
        return frameCount;
    }

    public static void RequireFinite(params float[] values)
    {
        foreach (float value in values)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidDataException("Ghost contains non-finite frame data.");
        }
    }
}
