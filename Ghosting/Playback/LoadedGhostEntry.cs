using TNRD.Zeepkist.GTR.Ghosting.Ghosts;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public readonly struct LoadedGhostEntry
{
    public LoadedGhostEntry(int recordId, string displayName, float duration, GhostData ghostData, IGhost ghost)
    {
        RecordId = recordId;
        DisplayName = displayName;
        Duration = duration;
        GhostData = ghostData;
        Ghost = ghost;
    }

    public int RecordId { get; }
    public string DisplayName { get; }
    public float Duration { get; }
    public GhostData GhostData { get; }
    public IGhost Ghost { get; }

    public string GetListLabel()
    {
        return $"{DisplayName} ({FormatDuration(Duration)})";
    }

    public static string FormatDuration(float seconds)
    {
        if (seconds < 0f)
            seconds = 0f;

        var minutes = (int)(seconds / 60f);
        var remainingSeconds = seconds - minutes * 60f;
        return $"{minutes:00}:{remainingSeconds:00.000}";
    }
}
