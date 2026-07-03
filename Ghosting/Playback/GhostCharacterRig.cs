using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public sealed class GhostCharacterRig
{
    private sealed class PoseSnapshot
    {
        public PoseSnapshot(Transform transform)
        {
            Transform = transform;
            LocalPosition = transform.localPosition;
            LocalRotation = transform.localRotation;
            LocalScale = transform.localScale;
            Name = transform.name.ToLowerInvariant();
        }

        public Transform Transform { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 LocalScale { get; }
        public string Name { get; }
    }

    private readonly GameObject _root;
    private readonly Vector3 _localPosition;
    private readonly Quaternion _localRotation;
    private readonly IReadOnlyList<PoseSnapshot> _poseSnapshots;

    private GhostCharacterRig(
        GameObject root,
        Vector3 localPosition,
        Quaternion localRotation,
        IReadOnlyList<PoseSnapshot> poseSnapshots)
    {
        _root = root;
        _localPosition = localPosition;
        _localRotation = localRotation;
        _poseSnapshots = poseSnapshots;
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
        return new GhostCharacterRig(root, localPosition, localRotation, CapturePose(root.transform));
    }

    public void AlignToSeated(Transform soapbox)
    {
        ApplySeatedPose();

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

    public void ApplySeatedPose()
    {
        ApplyPose(_poseSnapshots);
    }

    public void ApplyRagdollTPose()
    {
        ApplySeatedPose();

        foreach (PoseSnapshot snapshot in _poseSnapshots)
        {
            if (snapshot.Transform == null)
                continue;

            if (IsLeftArm(snapshot.Name))
            {
                snapshot.Transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            else if (IsRightArm(snapshot.Name))
            {
                snapshot.Transform.localRotation = Quaternion.Euler(0, 0, -90);
            }
            else if (IsLeg(snapshot.Name) || IsTorso(snapshot.Name))
            {
                snapshot.Transform.localRotation = Quaternion.identity;
            }
        }
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
        AddSkinnedRigParts(parts, model);

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

    private static IReadOnlyList<PoseSnapshot> CapturePose(Transform root)
    {
        var snapshots = new List<PoseSnapshot>();
        if (root == null)
            return snapshots;

        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            snapshots.Add(new PoseSnapshot(transform));

        return snapshots;
    }

    private static void ApplyPose(IEnumerable<PoseSnapshot> snapshots)
    {
        foreach (PoseSnapshot snapshot in snapshots)
        {
            if (snapshot.Transform == null)
                continue;

            snapshot.Transform.localPosition = snapshot.LocalPosition;
            snapshot.Transform.localRotation = snapshot.LocalRotation;
            snapshot.Transform.localScale = snapshot.LocalScale;
        }
    }

    private static bool IsTorso(string name) =>
        name.Contains("torso") ||
        name.Contains("body") ||
        name.Contains("chest") ||
        name.Contains("spine");

    private static bool IsLeftArm(string name) =>
        name.Contains("leftarm") ||
        name.Contains("left arm") ||
        name.Contains("l_arm") ||
        name.Contains("arm_l") ||
        name.Contains("left_arm");

    private static bool IsRightArm(string name) =>
        name.Contains("rightarm") ||
        name.Contains("right arm") ||
        name.Contains("r_arm") ||
        name.Contains("arm_r") ||
        name.Contains("right_arm");

    private static bool IsLeg(string name) =>
        name.Contains("leg") ||
        name.Contains("thigh") ||
        name.Contains("shin") ||
        name.Contains("foot");

    private static void AddSkinnedRigParts(ISet<Transform> parts, SetupModelCar model)
    {
        if (model == null)
            return;

        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!GhostCharacterRenderers.IsCharacterRenderer(renderer, model))
                continue;

            AddTopLevelChild(parts, renderer.rootBone, model.transform);
            foreach (Transform bone in renderer.bones)
                AddTopLevelChild(parts, bone, model.transform);
        }
    }

    private static void AddTopLevelChild(ISet<Transform> parts, Transform transform, Transform root)
    {
        if (transform == null || root == null)
            return;

        Transform current = transform;
        while (current.parent != null && current.parent != root)
            current = current.parent;

        if (current != root)
            parts.Add(current);
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
