using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostData
{
    public GhostData(GhostVisuals ghostVisuals)
    {
        Visuals = ghostVisuals;
    }

    public void Initialize(IGhost ghost)
    {
        Ghost = ghost;
    }

    public void InitializeRenderer()
    {
        Renderer = new GhostRenderer(Visuals.GhostModel.gameObject);
    }

    public IGhost Ghost { get; private set; }
    public GameObject GameObject => Visuals.GhostModel.gameObject;
    public GhostVisuals Visuals { get; private set; }
    public GhostRenderer Renderer { get; private set; }
    public RoyTheunissen.FMODSyntax.FmodAudioPlayback CurrentHorn { get; set; }
    public bool CurrentHornIsOneShot { get; set; }
    public FMOD_HornsIndex.HornType CurrentHornType { get; set; }
    public int CurrentHornTone { get; set; }
}
