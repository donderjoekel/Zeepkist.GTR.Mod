using System;

namespace TNRD.Zeepkist.GTR.Utilities;

public enum TextColour
{
    Yellow = 0xFACC15,
    Pink = 0xFB64B6,
    White = 0xFFFFFF
}

public static class TextColourExtensions
{
    public static string ToHex(this TextColour colour)
    {
        return $"#{(int)colour:X6}";
    }

    public static string Wrap(this TextColour colour, string value)
    {
        return $"<color={colour.ToHex()}>{value}</color>";
    }

    public static string InterpolateTo(this TextColour start, TextColour end, double progress)
    {
        progress = Math.Max(0, Math.Min(1, progress));
        int startValue = (int)start;
        int endValue = (int)end;
        int red = Interpolate((startValue >> 16) & 0xFF, (endValue >> 16) & 0xFF, progress);
        int green = Interpolate((startValue >> 8) & 0xFF, (endValue >> 8) & 0xFF, progress);
        int blue = Interpolate(startValue & 0xFF, endValue & 0xFF, progress);
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private static int Interpolate(int start, int end, double progress)
    {
        return (int)Math.Round(start + (end - start) * progress, MidpointRounding.AwayFromZero);
    }
}
