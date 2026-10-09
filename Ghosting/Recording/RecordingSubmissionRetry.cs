namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

internal static class RecordingSubmissionRetry
{
    internal static bool ShouldRetry(int status) => status == 408 || status == 429 || status >= 500;
    internal static int DelayMilliseconds(int attempt) => 500 << System.Math.Min(2, System.Math.Max(0, attempt));
}
