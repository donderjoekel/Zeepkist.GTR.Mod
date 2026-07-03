namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public readonly struct V6CharacterPlaybackState
{
    private V6CharacterPlaybackState(bool armsUpVisible, bool ragdollVisible)
    {
        ArmsUpVisible = armsUpVisible;
        RagdollVisible = ragdollVisible;
    }

    public static V6CharacterPlaybackState Reset => new(false, false);

    public bool SeatedVisible => !ArmsUpVisible && !RagdollVisible;
    public bool ArmsUpVisible { get; }
    public bool RagdollVisible { get; }

    public static V6CharacterPlaybackState FromSegment(
        bool previousRagdoll,
        bool nextRagdoll,
        bool armsUp)
    {
        bool ragdollVisible = previousRagdoll || nextRagdoll;
        return new V6CharacterPlaybackState(
            !ragdollVisible && armsUp,
            ragdollVisible);
    }
}
