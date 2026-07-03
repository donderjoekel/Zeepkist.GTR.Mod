using System.Collections.Generic;
using UnityEngine;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public sealed class GhostLimbPoseController
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

    private GhostLimbPoseController(
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

    public static GhostLimbPoseController Create(SetupModelCar model)
    {
        if (model == null)
            return Unavailable(model);

        NetworkedZeepkistGhost prefab = ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab;
        if (prefab == null || prefab.ghostModel == null)
            return Unavailable(model);

        Quaternion leftArmOffset = CreateRelativeRotation(prefab.downLeft, prefab.upLeft, ArmsUpBlend);
        Quaternion rightArmOffset = CreateRelativeRotation(prefab.downRight, prefab.upRight, ArmsUpBlend);
        GhostLimbPoseController controller = new(
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

    public static bool ApplySeatedArmsUpPose(SetupModelCar model)
    {
        GhostLimbPoseController controller = Create(model);
        if (!controller.IsAvailable)
            return false;

        controller.CaptureSeatedPose();
        controller.ApplySeated(true);
        return true;
    }

    public static bool ApplyStandingRagdollPose(SetupModelCar model)
    {
        GhostLimbPoseController controller = Create(model);
        if (!controller.IsAvailable)
            return false;

        controller.CaptureSeatedPose();
        controller.ApplyStandingRagdollPose();
        return true;
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

    private static GhostLimbPoseController Unavailable(SetupModelCar model)
    {
        LogUnavailable(model);
        return new GhostLimbPoseController(
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

    private static Transform ResolveTarget(
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
            Transform target = ResolveTarget(targetModel, prefabModel, prefabTarget, fallbackTarget);
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
