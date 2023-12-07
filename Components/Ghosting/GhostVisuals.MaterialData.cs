using UnityEngine;
using UnityEngine.Rendering;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting;

public partial class GhostVisuals
{
    private class MaterialData
    {
        private readonly Renderer renderer;
        private readonly Material[] normalMaterials;
        private readonly Material[] ghostMaterials;

        private bool isGhost;

        public MaterialData(Renderer renderer)
        {
            this.renderer = renderer;
            normalMaterials = this.renderer.materials;
            foreach (Material material in normalMaterials)
            {
                // material.SetOverrideTag("RenderType", "Transparent");
                // material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                // material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                // material.SetInt("_ZWrite", 0);
                // material.DisableKeyword("_ALPHATEST_ON");
                // material.EnableKeyword("_ALPHABLEND_ON");
                // material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                // material.renderQueue = (int)RenderQueue.Transparent;
            }

            ghostMaterials = new Material[normalMaterials.Length];
            for (int i = 0; i < ghostMaterials.Length; i++)
            {
                ghostMaterials[i] = new Material(NetworkedGhostSpawner.zeepkistGhostPrefab.ghostFader.fadeThisMaterial);
                // Material material = new Material(Shader.Find("Standard"));
                // material.SetOverrideTag("RenderType", "Transparent");
                // material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                // material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                // material.SetInt("_ZWrite", 0);
                // material.DisableKeyword("_ALPHATEST_ON");
                // material.EnableKeyword("_ALPHABLEND_ON");
                // material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                // material.renderQueue = (int)RenderQueue.Transparent;
                // ghostMaterials[i] = material;
            }
        }

        public void SetGhost(bool isGhost)
        {
            this.isGhost = isGhost;
            renderer.materials = this.isGhost ? ghostMaterials : normalMaterials;
        }

        public void Enable()
        {
            renderer.enabled = true;
        }

        public void Disable()
        {
            renderer.enabled = false;
        }

        public void SetGhostColor(Color fadedColor)
        {
            foreach (Material ghostMaterial in ghostMaterials)
            {
                ghostMaterial.color = fadedColor;
            }
        }

        public void SetNormalAlpha(float fadeAmount)
        {
            foreach (Material normalMaterial in normalMaterials)
            {
                normalMaterial.color = normalMaterial.color with
                {
                    a = fadeAmount
                };
            }
        }
    }
}
