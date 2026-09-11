using System;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

internal static class RecordSubmissionEligibility
{
    private const string TrulyRandomTrackLevelPrefix = "trtm-";

    internal static bool ShouldSubmit(string levelUid)
    {
        return levelUid == null ||
               !levelUid.StartsWith(TrulyRandomTrackLevelPrefix, StringComparison.Ordinal);
    }
}
