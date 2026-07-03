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
        GameObject bulkRagdollCharacterGameObject = null)
    {
        GameObject = gameObject;
        Visuals = ghostVisuals;
        VisualProfile = visualProfile;
        IsInstanced = isInstanced;
        BulkCharacterGameObject = bulkCharacterGameObject;
        BulkRagdollCharacterGameObject = bulkRagdollCharacterGameObject;
        Object.DontDestroyOnLoad(GameObject.transform.root.gameObject);
        if (BulkCharacterGameObject != null)
            Object.DontDestroyOnLoad(BulkCharacterGameObject.transform.root.gameObject);
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

    public void InitializeRenderer()
    {
        Renderer?.Dispose();
        Renderer = CharacterRig != null
            ? new GhostRenderer(Visuals.GhostModel.gameObject, CharacterRig.Root, VisualProfile)
            : new GhostRenderer(Visuals.GhostModel.gameObject, VisualProfile);
    }

    public void SetCharacterRig(GhostCharacterRig characterRig)
    {
        CharacterRig?.Destroy();
        CharacterRig = characterRig;
    }

    public void SetBulkCharacterLocalTransform(Vector3 localPosition, Quaternion localRotation)
    {
        BulkCharacterLocalPosition = localPosition;
        BulkCharacterLocalRotation = localRotation;
    }

    public void SetNameAnchor(Transform nameAnchor)
    {
        NameAnchor = nameAnchor != null ? nameAnchor : GameObject.transform;
    }

    public void SetBulkCharacterRagdollVisible(bool ragdollVisible)
    {
        BulkRagdollCharacterVisible = ragdollVisible;
        ApplyPlaybackVisibility();
    }

    public void SetPlaybackVisible(bool visible)
    {
        PlaybackVisible = visible;
        ApplyPlaybackVisibility();
    }

    public void ResetRenderState()
    {
        Renderer?.Enable();
        Renderer?.SwitchToNormal();
        Renderer?.SetFade(1);
        CharacterRig?.SetActive(true);
        SetBulkCharacterRagdollVisible(false);
        SetNameAnchor(GameObject.transform);
    }

    public IGhost Ghost { get; private set; }
    public GhostType Type { get; private set; }
    public GhostVisualProfile VisualProfile { get; }
    public bool IsInstanced { get; }
    public bool Active { get; private set; }
    public GameObject GameObject { get; }
    public GameObject BulkCharacterGameObject { get; }
    public GameObject BulkRagdollCharacterGameObject { get; }
    public Vector3 BulkCharacterLocalPosition { get; private set; }
    public Quaternion BulkCharacterLocalRotation { get; private set; } = Quaternion.identity;
    public bool PlaybackVisible { get; private set; }
    public bool BulkRagdollCharacterVisible { get; private set; }
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

        if (GameObject != null)
            GameObject.SetActive(visible);

        if (CharacterRig != null)
            CharacterRig.SetActive(visible);

        if (BulkCharacterGameObject != null)
            BulkCharacterGameObject.SetActive(visible && !BulkRagdollCharacterVisible);

        if (BulkRagdollCharacterGameObject != null)
            BulkRagdollCharacterGameObject.SetActive(visible && BulkRagdollCharacterVisible);

        if (!visible && Visuals?.NameDisplay != null)
            Visuals.NameDisplay.gameObject.SetActive(false);
    }
}
