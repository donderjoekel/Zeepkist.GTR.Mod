using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostData
{
    public GhostData(
        GameObject gameObject,
        GhostVisuals ghostVisuals,
        GhostVisualProfile visualProfile,
        bool isInstanced,
        GameObject bulkCharacterGameObject = null,
        GameObject bulkArmsUpCharacterGameObject = null,
        GameObject bulkRagdollCharacterGameObject = null)
    {
        GameObject = gameObject;
        Visuals = ghostVisuals;
        VisualProfile = visualProfile;
        IsInstanced = isInstanced;
        BulkCharacterGameObject = bulkCharacterGameObject;
        BulkArmsUpCharacterGameObject = bulkArmsUpCharacterGameObject;
        BulkRagdollCharacterGameObject = bulkRagdollCharacterGameObject;
        Object.DontDestroyOnLoad(GameObject.transform.root.gameObject);
        if (BulkCharacterGameObject != null)
            Object.DontDestroyOnLoad(BulkCharacterGameObject.transform.root.gameObject);
        if (BulkArmsUpCharacterGameObject != null)
            Object.DontDestroyOnLoad(BulkArmsUpCharacterGameObject.transform.root.gameObject);
        if (BulkRagdollCharacterGameObject != null)
            Object.DontDestroyOnLoad(BulkRagdollCharacterGameObject.transform.root.gameObject);
    }

    public void Initialize(GhostType type)
    {
        Type = type;
    }

    public void Initialize(GhostType type, IGhost ghost)
    {
        Initialize(type);
        Ghost = ghost;
    }

    public void SetIdentity(int recordId, string displayName)
    {
        RecordId = recordId;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? $"Ghost #{recordId}"
            : displayName;
    }

    public void ClearIdentity()
    {
        RecordId = 0;
        DisplayName = null;
    }

    public void PrepareForCosmeticsReuse()
    {
        DisposeRenderer();
        ClearCharacterRig(true);
        SetNameAnchor(GameObject.transform);
    }

    public void InitializeRenderer()
    {
        Renderer?.Dispose();
        Renderer = CharacterRig != null
            ? new GhostRenderer(Visuals.GhostModel.gameObject, CharacterRig.Root, VisualProfile)
            : new GhostRenderer(Visuals.GhostModel.gameObject, VisualProfile);
    }

    public void SetCharacterRig(GhostCharacterRig characterRig)
    {
        ClearCharacterRig(true);
        CharacterRig = characterRig;
        ApplyPlaybackVisibility();
    }

    public void ClearCharacterRig(bool restoreToModel)
    {
        if (CharacterRig == null)
            return;

        if (restoreToModel)
            CharacterRig.RestoreToModel();

        CharacterRig.Destroy();
        CharacterRig = null;
    }

    public void SetBulkRagdollRotationOffset(Quaternion rotationOffset)
    {
        BulkRagdollRotationOffset = rotationOffset;
    }

    public void SetNameAnchor(Transform nameAnchor)
    {
        NameAnchor = nameAnchor != null ? nameAnchor : GameObject.transform;
    }

    public void SetCharacterPlaybackState(GhostCharacterPlaybackState state)
    {
        if (CharacterPlaybackState.Equals(state))
            return;

        CharacterPlaybackState = state;
        ApplyPlaybackVisibility();
    }

    public void SetPlaybackVisible(bool visible)
    {
        if (PlaybackVisible == visible)
            return;

        PlaybackVisible = visible;
        ApplyPlaybackVisibility();
    }

    public void ResetPlaybackState()
    {
        PlaybackVisible = false;
        CharacterPlaybackState = GhostCharacterPlaybackState.Reset;
        Renderer?.Enable();
        Renderer?.SwitchToNormal();
        Renderer?.SetFade(1);
        CharacterRig?.ApplySeatedPose(false);
        CharacterRig?.SetActive(true);
        SetNameAnchor(GameObject.transform);
        ApplyPlaybackVisibility();
    }

    public IGhost Ghost { get; private set; }
    public GhostType Type { get; private set; }
    public int RecordId { get; private set; }
    public string DisplayName { get; private set; }
    public GhostVisualProfile VisualProfile { get; }
    public bool IsInstanced { get; }
    public bool Active { get; private set; }
    public GameObject GameObject { get; }
    public GameObject BulkCharacterGameObject { get; }
    public GameObject BulkArmsUpCharacterGameObject { get; }
    public GameObject BulkRagdollCharacterGameObject { get; }
    public Quaternion BulkRagdollRotationOffset { get; private set; } = Quaternion.identity;
    public bool PlaybackVisible { get; private set; }
    public GhostCharacterPlaybackState CharacterPlaybackState { get; private set; } = GhostCharacterPlaybackState.Reset;
    public Transform NameAnchor { get; private set; }
    public GhostCharacterRig CharacterRig { get; private set; }
    public GhostVisuals Visuals { get; private set; }
    public GhostRenderer Renderer { get; private set; }
    public RoyTheunissen.FMODSyntax.FmodAudioPlayback CurrentHorn { get; set; }
    public bool CurrentHornIsOneShot { get; set; }
    public FMOD_HornsIndex.HornType CurrentHornType { get; set; }
    public int CurrentHornTone { get; set; }

    public void DisposeRenderer()
    {
        Renderer?.Dispose();
        Renderer = null;
    }

    public void SetActive(bool active)
    {
        Active = active;
        if (Visuals != null)
            Visuals.gameObject.SetActive(active);

        ApplyPlaybackVisibility();
    }

    private void ApplyPlaybackVisibility()
    {
        bool visible = Active && PlaybackVisible;

        if (IsInstanced)
        {
            if (GameObject != null)
                GameObject.SetActive(visible);

            if (BulkCharacterGameObject != null)
                BulkCharacterGameObject.SetActive(visible && CharacterPlaybackState.SeatedVisible);

            if (BulkArmsUpCharacterGameObject != null)
                BulkArmsUpCharacterGameObject.SetActive(visible && CharacterPlaybackState.ArmsUpVisible);

            if (BulkRagdollCharacterGameObject != null)
                BulkRagdollCharacterGameObject.SetActive(visible && CharacterPlaybackState.RagdollVisible);
        }
        else
        {
            if (GameObject != null)
                GameObject.SetActive(Active);

            if (CharacterRig != null)
                CharacterRig.SetActive(Active && visible);

            if (visible)
                Renderer?.Enable();
            else
                Renderer?.Disable();
        }

        if (!visible && Visuals?.NameDisplay != null)
            Visuals.NameDisplay.gameObject.SetActive(false);
    }
}