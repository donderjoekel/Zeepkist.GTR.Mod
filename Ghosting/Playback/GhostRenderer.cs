using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public partial class GhostRenderer
{
    private readonly List<RendererData> _rendererData = new();

    public GhostRenderer(GameObject gameObject)
    {
        Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
        renderers.ToList().ForEach(renderer => _rendererData.Add(new RendererData(renderer)));
    }

    public void SwitchToNormal()
    {
        _rendererData.ForEach(rendererData => rendererData.SwitchToNormal());
    }

    public void SwitchToGhost()
    {
        _rendererData.ForEach(rendererData => rendererData.SwitchToGhost());
    }

    public void Enable()
    {
        _rendererData.ForEach(rendererData => rendererData.Enable());
    }

    public void Disable()
    {
        _rendererData.ForEach(rendererData => rendererData.Disable());
    }

    public void SetFade(float fade)
    {
        _rendererData.ForEach(rendererData => rendererData.SetFade(fade));
    }

    public void SetGhostColor(Color color)
    {
        _rendererData.ForEach(rendererData => rendererData.SetGhostColor(color));
    }
}
