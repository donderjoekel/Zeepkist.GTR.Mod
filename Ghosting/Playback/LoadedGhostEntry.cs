using TNRD.Zeepkist.GTR.Ghosting.Ghosts;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public readonly struct LoadedGhostEntry
{
    public LoadedGhostEntry(int recordId, string displayName, GhostData ghostData, IGhost ghost)
    {
        RecordId = recordId;
        DisplayName = displayName;
        GhostData = ghostData;
        Ghost = ghost;
    }

    public int RecordId { get; }
    public string DisplayName { get; }
    public GhostData GhostData { get; }
    public IGhost Ghost { get; }
}
