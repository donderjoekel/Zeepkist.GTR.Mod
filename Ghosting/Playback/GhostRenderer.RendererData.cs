using UnityEngine;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public partial class GhostRenderer
{
    private class RendererData
    {
        private readonly Renderer _renderer;
        private readonly Material[] _normalMaterials;
        private readonly Material[] _ghostMaterials;

        public RendererData(Renderer renderer)
        {
            _renderer = renderer;
            _normalMaterials = _renderer.materials;
            _ghostMaterials = new Material[_normalMaterials.Length];

            for (int i = 0; i < _normalMaterials.Length; i++)
            {
                _ghostMaterials[i] = new Material(
                    ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostFader.fadeThisMaterial);
            }
        }

        public void SwitchToNormal()
        {
            _renderer.sharedMaterials = _normalMaterials;
        }

        public void SwitchToGhost()
        {
            _renderer.sharedMaterials = _ghostMaterials;
        }

        public void Enable()
        {
            _renderer.enabled = true;
        }

        public void Disable()
        {
            _renderer.enabled = false;
        }

        public void SetFade(float fade)
        {
            foreach (Material normalMaterial in _normalMaterials)
            {
                normalMaterial.color = normalMaterial.color with
                {
                    a = fade
                };
            }
        }

        public void SetGhostColor(Color color)
        {
            foreach (Material ghostMaterial in _ghostMaterials)
            {
                ghostMaterial.color = color;
            }
        }
    }
}
