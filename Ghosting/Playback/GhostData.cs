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
        GameObject bulkCharacterGameObject = null)
    {
        GameObject = gameObject;
        Visuals = ghostVisuals;
        VisualProfile = visualProfile;
        IsInstanced = isInstanced;
        BulkCharacterGameObject = bulkCharacterGameObject;
        Object.DontDestroyOnLoad(GameObject.transform.root.gameObject);
        if (BulkCharacterGameObject != null)
            Object.DontDestroyOnLoad(BulkCharacterGameObject.transform.root.gameObject);
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

    public IGhost Ghost { get; private set; }
    public GhostType Type { get; private set; }
    public GhostVisualProfile VisualProfile { get; }
    public bool IsInstanced { get; }
    public GameObject GameObject { get; }
    public GameObject BulkCharacterGameObject { get; }
    public Vector3 BulkCharacterLocalPosition { get; private set; }
    public Quaternion BulkCharacterLocalRotation { get; private set; } = Quaternion.identity;
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
        if (Visuals != null)
            Visuals.gameObject.SetActive(active);

        GameObject.SetActive(active);
        if (BulkCharacterGameObject != null)
            BulkCharacterGameObject.SetActive(active);
        CharacterRig?.SetActive(active);
    }
}
