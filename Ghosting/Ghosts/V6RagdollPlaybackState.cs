namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public readonly struct V6RagdollPlaybackState
{
    private V6RagdollPlaybackState(bool seatedCharacterVisible, bool ragdollVisible)
    {
        SeatedCharacterVisible = seatedCharacterVisible;
        RagdollVisible = ragdollVisible;
    }

    public bool SeatedCharacterVisible { get; }
    public bool RagdollVisible { get; }

    public static V6RagdollPlaybackState Reset => new(true, false);

    public static V6RagdollPlaybackState FromSegment(bool previousRagdoll, bool nextRagdoll)
    {
        bool ragdollVisible = previousRagdoll || nextRagdoll;
        return new V6RagdollPlaybackState(!ragdollVisible, ragdollVisible);
    }
}
