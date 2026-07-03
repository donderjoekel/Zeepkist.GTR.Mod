using System;
using UnityEngine;
using ZeepSDK.Utilities;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public partial class GhostRenderer
{
    private class RendererData : IDisposable
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static Material _bulkGhostMaterial;

        private readonly Renderer _renderer;
        private readonly Material[] _normalMaterials;
        private readonly Material[] _ghostMaterials;
        private readonly bool _ownsMaterials;
        private readonly bool _useNormalMaterialsInGhostMode;

        public RendererData(
            Renderer renderer,
            GhostVisualProfile visualProfile,
            bool useNormalMaterialsInGhostMode)
        {
            _renderer = renderer;
            _ownsMaterials = visualProfile == GhostVisualProfile.Full;
            _useNormalMaterialsInGhostMode = useNormalMaterialsInGhostMode;
            _normalMaterials = _ownsMaterials
                ? _renderer.materials
                : _renderer.sharedMaterials;
            _ghostMaterials = new Material[_normalMaterials.Length];

            if (_ownsMaterials)
            {
                for (int i = 0; i < _normalMaterials.Length; i++)
                {
                    _ghostMaterials[i] = new Material(
                        ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostFader.fadeThisMaterial);
                }
            }
            else
            {
                Material bulkMaterial = GetBulkGhostMaterial();
                for (int i = 0; i < _ghostMaterials.Length; i++)
                    _ghostMaterials[i] = bulkMaterial;
            }
        }

        public void SwitchToNormal()
        {
            if (_renderer == null)
                return;

            if (_ownsMaterials)
                _renderer.materials = _normalMaterials;
            else
                _renderer.sharedMaterials = _normalMaterials;
        }

        public void SwitchToGhost()
        {
            if (_renderer == null)
                return;

            if (_useNormalMaterialsInGhostMode)
            {
                SwitchToNormal();
                return;
            }

            if (_ownsMaterials)
                _renderer.materials = _ghostMaterials;
            else
                _renderer.sharedMaterials = _ghostMaterials;
        }

        public void Enable()
        {
            if (_renderer != null)
                _renderer.enabled = true;
        }

        public void Disable()
        {
            if (_renderer != null)
                _renderer.enabled = false;
        }

        public void SetFade(float fade)
        {
            if (!_ownsMaterials)
                return;

            foreach (Material normalMaterial in _normalMaterials)
            {
                if (!normalMaterial.HasProperty(ColorId))
                    continue;

                normalMaterial.color = normalMaterial.color with
                {
                    a = fade
                };
            }
        }

        public void SetGhostColor(Color color)
        {
            if (!_ownsMaterials)
                return;

            if (_useNormalMaterialsInGhostMode)
            {
                SetFade(color.a);
                return;
            }

            foreach (Material ghostMaterial in _ghostMaterials)
            {
                if (!ghostMaterial.HasProperty(ColorId))
                    continue;

                ghostMaterial.color = color;
            }
        }

        public void Dispose()
        {
            if (!_ownsMaterials)
                return;

            foreach (Material material in _normalMaterials)
            {
                if (material != null)
                    Object.Destroy(material);
            }

            foreach (Material material in _ghostMaterials)
            {
                if (material != null)
                    Object.Destroy(material);
            }
        }

        public static void DisposeSharedResources()
        {
            if (_bulkGhostMaterial == null)
                return;

            Object.Destroy(_bulkGhostMaterial);
            _bulkGhostMaterial = null;
        }

        private static Material GetBulkGhostMaterial()
        {
            if (_bulkGhostMaterial != null)
                return _bulkGhostMaterial;

            _bulkGhostMaterial = new Material(
                ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostFader.fadeThisMaterial)
            {
                enableInstancing = true,
                color = Color.white with { a = 0.3f }
            };
            return _bulkGhostMaterial;
        }
    }
}
