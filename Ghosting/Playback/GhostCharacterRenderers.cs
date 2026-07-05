using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public static class GhostCharacterRenderers
{
    public static void SetCharacterRenderersActive(SetupModelCar model, bool active)
    {
        if (model == null)
            return;

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (IsCharacterRenderer(renderer, model))
                renderer.enabled = active;
        }
    }

    public static void SetNonCharacterRenderersActive(SetupModelCar model, bool active)
    {
        if (model == null)
            return;

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!IsCharacterRenderer(renderer, model))
                renderer.enabled = active;
        }
    }

    public static bool IsCharacterRenderer(Renderer renderer, SetupModelCar model)
    {
        if (renderer == null)
            return false;

        if (model == null)
            return IsCharacterRenderer(renderer.transform, null);

        if (IsRendererOrChild(renderer, model.character) ||
            IsRendererOrChild(renderer, model.leftArm) ||
            IsRendererOrChild(renderer, model.rightArm) ||
            IsRendererOrChild(renderer, model.leftLeg) ||
            IsRendererOrChild(renderer, model.rightLeg))
        {
            return true;
        }

        if (model.auxObjects != null && renderer.transform.IsChildOf(model.auxObjects))
        {
            string auxPath = GetTransformPath(renderer.transform).ToLowerInvariant();
            if (auxPath.Contains("face") ||
                auxPath.Contains("smile") ||
                auxPath.Contains("heart") ||
                auxPath.Contains("head"))
            {
                return true;
            }
        }

        return IsCharacterRenderer(renderer.transform, model.character);
    }

    public static bool IsCharacterRenderer(Transform transform, SkinnedMeshRenderer character)
    {
        if (transform == null)
            return false;

        if (character != null && transform.IsChildOf(character.transform))
            return true;

        string path = GetTransformPath(transform).ToLowerInvariant();
        return path.Contains("character") ||
               path.Contains("player") ||
               path.Contains("torso") ||
               path.Contains("body") ||
               path.Contains("head") ||
               path.Contains("face") ||
               path.Contains("smile") ||
               path.Contains("heart") ||
               path.Contains("arm") ||
               path.Contains("leg") ||
               path.Contains("hand") ||
               path.Contains("foot");
    }

    private static bool IsRendererOrChild(Renderer renderer, Renderer characterRenderer)
    {
        return characterRenderer != null &&
               (renderer == characterRenderer || renderer.transform.IsChildOf(characterRenderer.transform));
    }

    private static string GetTransformPath(Transform transform)
    {
        string path = transform.name;
        Transform parent = transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}
