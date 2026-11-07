// Headless startup lifecycle data. Production readiness/header capture code is linked unchanged.
public sealed class PlayerManager
{
    public static PlayerManager Instance { get; set; }
    public StartupGameVersion version;
    public PlaybackGameMaster currentMaster;
}

public sealed class PlaybackGameMaster { public bool isPhotoMode; }

public sealed class StartupGameVersion
{
    public int version;
    public int patch;
}
