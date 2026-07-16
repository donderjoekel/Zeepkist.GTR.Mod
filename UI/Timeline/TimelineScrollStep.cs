namespace TNRD.Zeepkist.GTR.UI.Timeline;

internal static class TimelineScrollStep
{
    internal const float DefaultSeconds = 1f;
    internal const float ShiftSeconds = 10f;
    internal const float ControlSeconds = 0.1f;
    internal const int AltFrames = 10;
    internal const int AltShiftFrames = 100;
    internal const int AltControlFrames = 1;

    internal static void GetScrubScrollStep(
        bool alt,
        bool shift,
        bool control,
        out bool useFrames,
        out float seconds,
        out int frameCount)
    {
        if (alt)
        {
            useFrames = true;
            seconds = 0f;
            frameCount = control ? AltControlFrames : shift ? AltShiftFrames : AltFrames;
            return;
        }

        useFrames = false;
        frameCount = 0;
        seconds = shift ? ShiftSeconds : control ? ControlSeconds : DefaultSeconds;
    }
}
