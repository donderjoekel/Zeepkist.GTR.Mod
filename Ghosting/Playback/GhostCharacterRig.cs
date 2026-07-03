using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public sealed class GhostCharacterRig
{
    private readonly GameObject _root;
    private readonly Vector3 _localPosition;
    private readonly Quaternion _localRotation;

    private GhostCharacterRig(GameObject root, Vector3 localPosition, Quaternion localRotation)
    {
        _root = root;
        _localPosition = localPosition;
        _localRotation = localRotation;
    }

    public GameObject Root => _root;

    public static GhostCharacterRig Create(SetupModelCar model)
    {
        if (model == null)
            return null;

        Transform sourceRoot = GetSourceRoot(model);
        if (sourceRoot == null)
            return null;

        var root = new GameObject("Ghost Character Rig");
        Object.DontDestroyOnLoad(root.transform.root.gameObject);
        root.transform.SetPositionAndRotation(sourceRoot.position, sourceRoot.rotation);

        foreach (Transform part in GetTopLevelCharacterParts(model))
            part.SetParent(root.transform, true);

        Vector3 localPosition = model.transform.InverseTransformPoint(sourceRoot.position);
        Quaternion localRotation = Quaternion.Inverse(model.transform.rotation) * sourceRoot.rotation;
        return new GhostCharacterRig(root, localPosition, localRotation);
    }

    public void AlignToSeated(Transform soapbox)
    {
        if (soapbox == null)
            return;

        AlignToWorld(
            soapbox.TransformPoint(_localPosition),
            soapbox.rotation * _localRotation);
    }

    public void AlignToWorld(Vector3 position, Quaternion rotation)
    {
        if (_root == null)
            return;

        _root.transform.SetPositionAndRotation(position, rotation);
    }

    public void SetActive(bool active)
    {
        if (_root != null)
            _root.SetActive(active);
    }

    public void Destroy()
    {
        if (_root != null)
            Object.Destroy(_root);
    }

    private static Transform GetSourceRoot(SetupModelCar model)
    {
        if (model.character != null)
            return model.character.transform;

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (GhostCharacterRenderers.IsCharacterRenderer(renderer, model))
                return renderer.transform;
        }

        return null;
    }

    private static IEnumerable<Transform> GetTopLevelCharacterParts(SetupModelCar model)
    {
        var parts = new HashSet<Transform>();
        AddPart(parts, model.character);
        AddPart(parts, model.leftArm);
        AddPart(parts, model.rightArm);
        AddPart(parts, model.leftLeg);
        AddPart(parts, model.rightLeg);

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (GhostCharacterRenderers.IsCharacterRenderer(renderer, model))
                parts.Add(renderer.transform);
        }

        foreach (Transform part in parts)
        {
            if (!HasAncestorInSet(part, parts))
                yield return part;
        }
    }

    private static void AddPart(ISet<Transform> parts, Renderer renderer)
    {
        if (renderer != null)
            parts.Add(renderer.transform);
    }

    private static bool HasAncestorInSet(Transform transform, ISet<Transform> parts)
    {
        Transform parent = transform.parent;
        while (parent != null)
        {
            if (parts.Contains(parent))
                return true;

            parent = parent.parent;
        }

        return false;
    }
}
