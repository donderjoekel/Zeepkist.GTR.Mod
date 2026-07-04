using System.Collections.Generic;
using UnityEngine;
using ZeepSDK.Utilities;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public sealed class GhostCharacterRig
{
    private sealed class RigPart
    {
        public RigPart(Transform transform)
        {
            Transform = transform;
            OriginalParent = transform.parent;
            OriginalSiblingIndex = transform.GetSiblingIndex();
        }

        public Transform Transform { get; }
        public Transform OriginalParent { get; }
        public int OriginalSiblingIndex { get; }
    }

    private sealed class PoseSnapshot
    {
        public PoseSnapshot(Transform transform)
        {
            Transform = transform;
            LocalPosition = transform.localPosition;
            LocalRotation = transform.localRotation;
            LocalScale = transform.localScale;
        }

        public Transform Transform { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 LocalScale { get; }
    }

    private readonly GameObject _root;
    private readonly Vector3 _localPosition;
    private readonly Quaternion _localRotation;
    private readonly IReadOnlyList<PoseSnapshot> _poseSnapshots;
    private readonly IReadOnlyList<RigPart> _rigParts;
    private readonly LimbPoseController _limbPoseController;

    private GhostCharacterRig(
        GameObject root,
        Vector3 localPosition,
        Quaternion localRotation,
        IReadOnlyList<PoseSnapshot> poseSnapshots,
        IReadOnlyList<RigPart> rigParts,
        LimbPoseController limbPoseController)
    {
        _root = root;
        _localPosition = localPosition;
        _localRotation = localRotation;
        _poseSnapshots = poseSnapshots;
        _rigParts = rigParts;
        _limbPoseController = limbPoseController;
    }

    public GameObject Root => _root;
    public Quaternion RagdollRotationOffset => _localRotation;

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

        LimbPoseController limbPoseController = LimbPoseController.Create(model);
        var rigParts = new List<RigPart>();
        foreach (Transform part in GetTopLevelCharacterParts(model))
        {
            rigParts.Add(new RigPart(part));
            part.SetParent(root.transform, true);
        }

        Vector3 localPosition = model.transform.InverseTransformPoint(sourceRoot.position);
        Quaternion localRotation = Quaternion.Inverse(model.transform.rotation) * sourceRoot.rotation;
        IReadOnlyList<PoseSnapshot> poseSnapshots = CapturePose(root.transform);
        limbPoseController.CaptureSeatedPose();
        return new GhostCharacterRig(
            root,
            localPosition,
            localRotation,
            poseSnapshots,
            rigParts,
            limbPoseController);
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

    public Quaternion GetRagdollWorldRotation(Quaternion recordedRotation)
    {
        return recordedRotation * RagdollRotationOffset;
    }

    public void ApplySeatedPose(bool armsUp = false)
    {
        ApplyPose(_poseSnapshots);
        _limbPoseController?.ApplySeated(armsUp);
    }

    public void ApplyStandingRagdollPose()
    {
        ApplySeatedPose();
        _limbPoseController?.ApplyStandingRagdollPose();
    }

    public void SetActive(bool active)
    {
        if (_root != null)
            _root.SetActive(active);
    }

    public void RestoreToModel()
    {
        ApplySeatedPose(false);
        foreach (RigPart rigPart in _rigParts)
        {
            if (rigPart.Transform == null || rigPart.OriginalParent == null)
                continue;

            rigPart.Transform.SetParent(rigPart.OriginalParent, true);
            rigPart.Transform.SetSiblingIndex(rigPart.OriginalSiblingIndex);
        }
    }

    public void Destroy()
    {
        if (_root != null)
            Object.Destroy(_root);
    }

    public static bool ApplySeatedArmsUpPose(SetupModelCar model)
    {
        LimbPoseController controller = LimbPoseController.Create(model);
        if (!controller.IsAvailable)
            return false;

        controller.CaptureSeatedPose();
        controller.ApplySeated(true);
        return true;
    }

    public static bool ApplyStandingRagdollPose(SetupModelCar model)
    {
        LimbPoseController controller = LimbPoseController.Create(model);
        if (!controller.IsAvailable)
            return false;

        controller.CaptureSeatedPose();
        controller.ApplyStandingRagdollPose();
        return true;
    }

    public static Quaternion GetRagdollRotationOffset(SetupModelCar model)
    {
        Transform sourceRoot = GetSourceRoot(model);
        return sourceRoot != null && model != null
            ? Quaternion.Inverse(model.transform.rotation) * sourceRoot.rotation
            : Quaternion.identity;
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
        AddPart(parts, model.hatParent);
        foreach (Transform poseTarget in LimbPoseController.GetPoseTargets(model))
            AddPart(parts, poseTarget);
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

    private static void AddPart(ISet<Transform> parts, Transform transform)
    {
        if (transform != null)
            parts.Add(transform);
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

    private sealed class LimbPoseController
    {
        private const float ArmsUpBlend = 0.35f;
        private const float RagdollArmHorizontal = 1.6f;
        private const float RagdollArmVertical = 0.6f;
        private const float RagdollArmForward = 0.0f;
        private const float RagdollLegHorizontal = 0.48f;
        private const float RagdollLegVertical = -1.2f;
        private const float RagdollLegForward = 0.0f;
        private static bool _loggedUnavailable;

        private readonly LimbPose _leftArm;
        private readonly LimbPose _rightArm;
        private readonly LimbPose _leftLeg;
        private readonly LimbPose _rightLeg;

        private LimbPoseController(
            LimbPose leftArm,
            LimbPose rightArm,
            LimbPose leftLeg,
            LimbPose rightLeg)
        {
            _leftArm = leftArm;
            _rightArm = rightArm;
            _leftLeg = leftLeg;
            _rightLeg = rightLeg;
        }

        public bool IsAvailable =>
            _leftArm.IsAvailable ||
            _rightArm.IsAvailable ||
            _leftLeg.IsAvailable ||
            _rightLeg.IsAvailable;

        public static LimbPoseController Create(SetupModelCar model)
        {
            if (model == null)
                return Unavailable(model);

            NetworkedZeepkistGhost prefab = ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab;
            if (prefab == null || prefab.ghostModel == null)
                return Unavailable(model);

            Quaternion leftArmOffset = CreateRelativeRotation(prefab.downLeft, prefab.upLeft, ArmsUpBlend);
            Quaternion rightArmOffset = CreateRelativeRotation(prefab.downRight, prefab.upRight, ArmsUpBlend);
            LimbPoseController controller = new(
                LimbPose.Create(
                    model,
                    prefab.ghostModel,
                    prefab.visualLeftArm,
                    model.leftArm?.transform,
                    leftArmOffset,
                    CreateRagdollLocalPosition(model, model.leftArm?.transform, -1, RagdollArmHorizontal, RagdollArmVertical, RagdollArmForward)),
                LimbPose.Create(
                    model,
                    prefab.ghostModel,
                    prefab.visualRightArm,
                    model.rightArm?.transform,
                    rightArmOffset,
                    CreateRagdollLocalPosition(model, model.rightArm?.transform, 1, RagdollArmHorizontal, RagdollArmVertical, RagdollArmForward)),
                LimbPose.Create(
                    model,
                    prefab.ghostModel,
                    prefab.visualLeftLeg,
                    model.leftLeg?.transform,
                    CreateLegStandingRotation(model.leftLeg?.transform),
                    CreateRagdollLocalPosition(model, model.leftLeg?.transform, -1, RagdollLegHorizontal, RagdollLegVertical, RagdollLegForward)),
                LimbPose.Create(
                    model,
                    prefab.ghostModel,
                    prefab.visualRightLeg,
                    model.rightLeg?.transform,
                    CreateLegStandingRotation(model.rightLeg?.transform),
                    CreateRagdollLocalPosition(model, model.rightLeg?.transform, 1, RagdollLegHorizontal, RagdollLegVertical, RagdollLegForward)));

            if (!controller.IsAvailable)
                LogUnavailable(model);

            return controller;
        }

        public static IEnumerable<Transform> GetPoseTargets(SetupModelCar model)
        {
            if (model == null)
                yield break;

            NetworkedZeepkistGhost prefab = ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab;
            if (prefab == null || prefab.ghostModel == null)
                yield break;

            Transform leftArm = ResolveTarget(model, prefab.ghostModel, prefab.visualLeftArm, model.leftArm?.transform);
            if (leftArm != null)
                yield return leftArm;

            Transform rightArm = ResolveTarget(model, prefab.ghostModel, prefab.visualRightArm, model.rightArm?.transform);
            if (rightArm != null)
                yield return rightArm;

            Transform leftLeg = ResolveTarget(model, prefab.ghostModel, prefab.visualLeftLeg, model.leftLeg?.transform);
            if (leftLeg != null)
                yield return leftLeg;

            Transform rightLeg = ResolveTarget(model, prefab.ghostModel, prefab.visualRightLeg, model.rightLeg?.transform);
            if (rightLeg != null)
                yield return rightLeg;
        }

        public void CaptureSeatedPose()
        {
            _leftArm.CaptureSeatedPose();
            _rightArm.CaptureSeatedPose();
            _leftLeg.CaptureSeatedPose();
            _rightLeg.CaptureSeatedPose();
        }

        public void ApplySeated(bool armsUp)
        {
            _leftArm.ApplyRotationOffset(armsUp);
            _rightArm.ApplyRotationOffset(armsUp);
            _leftLeg.ApplySeated();
            _rightLeg.ApplySeated();
        }

        public void ApplyStandingRagdollPose()
        {
            _leftArm.ApplyPosePositionAndRotation();
            _rightArm.ApplyPosePositionAndRotation();
            _leftLeg.ApplyPosePositionAndRotation();
            _rightLeg.ApplyPosePositionAndRotation();
        }

        private static LimbPoseController Unavailable(SetupModelCar model)
        {
            LogUnavailable(model);
            return new LimbPoseController(
                LimbPose.Unavailable,
                LimbPose.Unavailable,
                LimbPose.Unavailable,
                LimbPose.Unavailable);
        }

        private static void LogUnavailable(SetupModelCar model)
        {
            if (_loggedUnavailable)
                return;

            _loggedUnavailable = true;
            Debug.LogWarning($"GTR ghost limb pose unavailable for model '{model?.name ?? "null"}'.");
        }

        private static Quaternion CreateRelativeRotation(Transform from, Transform to, float blend)
        {
            if (from == null || to == null)
                return Quaternion.identity;

            return Quaternion.Slerp(
                Quaternion.identity,
                Quaternion.Inverse(from.localRotation) * to.localRotation,
                blend);
        }

        private static Quaternion CreateLegStandingRotation(Transform fallbackTarget)
        {
            if (fallbackTarget == null)
                return Quaternion.identity;

            Vector3 currentDirection = fallbackTarget.localRotation * Vector3.forward;
            return Quaternion.FromToRotation(currentDirection, Vector3.down);
        }

        private static PoseLocalPosition CreateRagdollLocalPosition(
            SetupModelCar model,
            Transform target,
            int side,
            float horizontal,
            float vertical,
            float forward)
        {
            if (model?.character == null || target?.parent == null)
                return PoseLocalPosition.Unavailable;

            Bounds bounds = model.character.bounds;
            Vector3 worldPosition = bounds.center +
                                    model.transform.right * (bounds.extents.x * horizontal * side) +
                                    Vector3.up * (bounds.extents.y * vertical) +
                                    model.transform.forward * (bounds.extents.z * forward);
            return new PoseLocalPosition(true, target.parent.InverseTransformPoint(worldPosition));
        }

        public static Transform ResolveTarget(
            SetupModelCar targetModel,
            SetupModelCar prefabModel,
            Transform prefabTransform,
            Transform fallback)
        {
            Transform resolved = Resolve(targetModel, prefabModel, prefabTransform);
            return resolved != null ? resolved : fallback;
        }

        private static Transform Resolve(SetupModelCar targetModel, SetupModelCar prefabModel, Transform prefabTransform)
        {
            if (targetModel == null || prefabModel == null || prefabTransform == null)
                return null;

            string path = GetRelativePath(prefabModel.transform, prefabTransform);
            if (string.IsNullOrEmpty(path))
                return null;

            return targetModel.transform.Find(path);
        }

        private static string GetRelativePath(Transform root, Transform child)
        {
            if (root == null || child == null || child == root)
                return null;

            string path = child.name;
            Transform current = child.parent;
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return current == root ? path : null;
        }
    }

    private readonly struct PoseLocalPosition
    {
        public static PoseLocalPosition Unavailable => new(false, Vector3.zero);

        public PoseLocalPosition(bool available, Vector3 value)
        {
            Available = available;
            Value = value;
        }

        public bool Available { get; }
        public Vector3 Value { get; }
    }

    private sealed class LimbPose
    {
        public static LimbPose Unavailable { get; } = new(null, Quaternion.identity, PoseLocalPosition.Unavailable);

        private readonly Transform _target;
        private readonly Quaternion _poseRotationOffset;
        private readonly PoseLocalPosition _poseLocalPosition;
        private Vector3 _seatedLocalPosition;
        private Quaternion _seatedLocalRotation;
        private Vector3 _seatedLocalScale;
        private bool _hasSeatedPose;

        private LimbPose(
            Transform target,
            Quaternion poseRotationOffset,
            PoseLocalPosition poseLocalPosition)
        {
            _target = target;
            _poseRotationOffset = poseRotationOffset;
            _poseLocalPosition = poseLocalPosition;
        }

        public bool IsAvailable => _target != null;

        public static LimbPose Create(
            SetupModelCar targetModel,
            SetupModelCar prefabModel,
            Transform prefabTarget,
            Transform fallbackTarget,
            Quaternion poseRotationOffset,
            PoseLocalPosition poseLocalPosition)
        {
            Transform target = LimbPoseController.ResolveTarget(targetModel, prefabModel, prefabTarget, fallbackTarget);
            return target != null ? new LimbPose(target, poseRotationOffset, poseLocalPosition) : Unavailable;
        }

        public void CaptureSeatedPose()
        {
            if (_target == null)
                return;

            _seatedLocalPosition = _target.localPosition;
            _seatedLocalRotation = _target.localRotation;
            _seatedLocalScale = _target.localScale;
            _hasSeatedPose = true;
        }

        public void ApplySeated()
        {
            if (_target == null || !_hasSeatedPose)
                return;

            _target.localPosition = _seatedLocalPosition;
            _target.localRotation = _seatedLocalRotation;
            _target.localScale = _seatedLocalScale;
        }

        public void ApplyRotationOffset(bool enabled)
        {
            if (_target == null || !_hasSeatedPose)
                return;

            _target.localPosition = _seatedLocalPosition;
            _target.localRotation = enabled
                ? _seatedLocalRotation * _poseRotationOffset
                : _seatedLocalRotation;
            _target.localScale = _seatedLocalScale;
        }

        public void ApplyPosePositionAndRotation()
        {
            if (_target == null || !_hasSeatedPose)
                return;

            _target.localPosition = _poseLocalPosition.Available
                ? _poseLocalPosition.Value
                : _seatedLocalPosition;
            _target.localRotation = _seatedLocalRotation * _poseRotationOffset;
            _target.localScale = _seatedLocalScale;
        }
    }
}