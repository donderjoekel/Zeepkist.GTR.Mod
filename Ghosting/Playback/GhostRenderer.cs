using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public partial class GhostRenderer : IDisposable
{
    private readonly List<RendererData> _rendererData = new();

    public GhostRenderer(GameObject gameObject, GhostVisualProfile visualProfile)
        : this(new[] { gameObject }, visualProfile)
    {
    }

    public GhostRenderer(GameObject gameObject, GameObject normalMaterialsInGhostGameObject, GhostVisualProfile visualProfile)
    {
        AddRenderers(gameObject, visualProfile, false);
        AddRenderers(normalMaterialsInGhostGameObject, visualProfile, true);
    }

    public GhostRenderer(IEnumerable<GameObject> gameObjects, GhostVisualProfile visualProfile)
    {
        foreach (GameObject gameObject in gameObjects)
            AddRenderers(gameObject, visualProfile, false);
    }

    private void AddRenderers(
        GameObject gameObject,
        GhostVisualProfile visualProfile,
        bool useNormalMaterialsInGhostMode)
    {
        if (gameObject == null)
            return;

        bool includeInactive = visualProfile == GhostVisualProfile.Full;
        Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(includeInactive);
        renderers.ToList().ForEach(renderer => _rendererData.Add(new RendererData(
            renderer,
            visualProfile,
            useNormalMaterialsInGhostMode)));
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

    public void Dispose()
    {
        _rendererData.ForEach(rendererData => rendererData.Dispose());
        _rendererData.Clear();
    }

    public static void DisposeSharedResources()
    {
        RendererData.DisposeSharedResources();
    }
}
