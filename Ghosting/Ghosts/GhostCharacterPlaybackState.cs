namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public enum GhostCharacterPlaybackPose
{
    Seated,
    SeatedArmsUp,
    Ragdoll
}

public readonly struct GhostCharacterPlaybackState
{
    private GhostCharacterPlaybackState(GhostCharacterPlaybackPose pose)
    {
        Pose = pose;
    }

    public static GhostCharacterPlaybackState Reset => new(GhostCharacterPlaybackPose.Seated);

    public GhostCharacterPlaybackPose Pose { get; }
    public bool SeatedVisible => Pose == GhostCharacterPlaybackPose.Seated;
    public bool ArmsUpVisible => Pose == GhostCharacterPlaybackPose.SeatedArmsUp;
    public bool RagdollVisible => Pose == GhostCharacterPlaybackPose.Ragdoll;

    public static GhostCharacterPlaybackState FromSeated(bool armsUp)
    {
        return new GhostCharacterPlaybackState(
            armsUp ? GhostCharacterPlaybackPose.SeatedArmsUp : GhostCharacterPlaybackPose.Seated);
    }

    public static GhostCharacterPlaybackState FromSegment(
        bool previousRagdoll,
        bool nextRagdoll,
        bool armsUp)
    {
        return previousRagdoll || nextRagdoll
            ? new GhostCharacterPlaybackState(GhostCharacterPlaybackPose.Ragdoll)
            : FromSeated(armsUp);
    }
}