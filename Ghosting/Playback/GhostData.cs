using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostData
{
    public GhostData(GhostVisuals ghostVisuals, GhostVisualProfile visualProfile)
    {
        Visuals = ghostVisuals;
        VisualProfile = visualProfile;
        Object.DontDestroyOnLoad(GameObject.transform.root.gameObject);
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
        Renderer = new GhostRenderer(Visuals.GhostModel.gameObject, VisualProfile);
    }

    public IGhost Ghost { get; private set; }
    public GhostType Type { get; private set; }
    public GhostVisualProfile VisualProfile { get; }
    public GameObject GameObject => Visuals.GhostModel.gameObject;
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
}
