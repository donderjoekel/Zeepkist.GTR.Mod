namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostData
{
    public GhostData(GhostVisuals ghostVisuals)
    {
        Visuals = ghostVisuals;
        Renderer = new GhostRenderer(Visuals.GhostModel.gameObject);
    }

    public GhostVisuals Visuals { get; private set; }
    public GhostRenderer Renderer { get; private set; }
}
