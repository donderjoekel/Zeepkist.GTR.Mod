using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Steamworks;
using TNRD.Zeepkist.GTR.Ghosting.Recording.Data;
using TNRD.Zeepkist.GTR.PlayerLoop;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;
using ZeepkistNetworking;
using ZeepSDK.External.Cysharp.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public partial class GhostRecorder
{
    private readonly struct RagdollFrameTransform
    {
        public RagdollFrameTransform(UnityEngine.Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public UnityEngine.Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }

    private const int FrameBlockSize = 512;

    private readonly PlayerLoopService _playerLoopService;
    private StructFrameBuffer<Frame> _frames = new(FrameBlockSize);
    private bool _sealed;
    private HashSet<string> _encounteredMaterialPhysicsNames;
    private HashSet<string> _unknownMaterialPhysicsNames;
    private bool _loggedSoapSurfaceOverride;
    private readonly ILogger<GhostRecorder> _logger;

    private PlayerLoopSubscription _updateToken;
    private PlayerLoopSubscription _fixedUpdateToken;
    private SetupCar _setupCar;
    private ReadyToReset _readyToReset;

    private bool _isBraking;
    private bool _isHorn;
    private bool _isArmsUp;
    private bool _isRagdoll;
    private Transform _ragdollRoot;
    private Rigidbody[] _ragdollRigidbodies;
    private Renderer[] _ragdollRenderers;

    public GhostRecorder(PlayerLoopService playerLoopService, ILogger<GhostRecorder> logger)
    {
        _playerLoopService = playerLoopService;
        _logger = logger;
    }

    public void Start()
    {
        if (_sealed || _updateToken != null)
            return;
        _updateToken = _playerLoopService.SubscribeUpdate(Update);
        _fixedUpdateToken = _playerLoopService.SubscribeFixedUpdate(FixedUpdate);

        _setupCar = PlayerManager.Instance.currentMaster.carSetups.FirstOrDefault();
        if (_setupCar == null)
        {
            _logger.LogError("No SetupCar found");
        }

        _readyToReset = PlayerManager.Instance.currentMaster.PlayersReady.FirstOrDefault();
        if (_readyToReset == null)
        {
            _logger.LogError("No ReadyToReset found");
        }
    }

    public void Stop()
    {
        if (_updateToken != null)
            _playerLoopService.UnsubscribeUpdate(_updateToken);
        if (_fixedUpdateToken != null)
            _playerLoopService.UnsubscribeFixedUpdate(_fixedUpdateToken);

        _updateToken = null;
        _fixedUpdateToken = null;
    }

    private void Update()
    {
        if (_setupCar == null || _readyToReset == null)
            return;

        New_ControlCar cc = _setupCar.cc;
        _isBraking = cc.BrakeAction2.buttonHeld;
        _isHorn = cc.IsHorning();
        _isArmsUp = cc.ArmsUpAction2.buttonHeld;
    }

    private void FixedUpdate()
    {
        if (_setupCar == null || _readyToReset == null)
            return;

        CaptureFrame(_readyToReset.ticker.what_ticker);
    }

    public void CaptureFinishFrame(float finishTime)
    {
        if (_setupCar == null || _readyToReset == null)
            return;

        if (_frames.Count > 0 && _frames[^1].Time >= finishTime)
            return;

        CaptureFrame(finishTime);
    }

    private void CaptureFrame(float time)
    {
        if (_sealed)
            return;
        if (_frames.Count >= GhostLimits.MaxFrames)
        {
            _logger.LogWarning("Ghost frame limit reached; stopping recording");
            Stop();
            return;
        }

        Transform carTransform = _setupCar.transform;
        New_ControlCar cc = _setupCar.cc;
        UnityEngine.Vector3 localVelocity = cc.GetLocalVelocity();
        UnityEngine.Vector3 localAngularVelocity = cc.GetLocalAngularVelocity();
        UnityEngine.Vector2 localGForce = cc.GetGForce();
        float speed = localVelocity.magnitude * 3.6f;
        SampleWheels(cc, out WheelState wheelState, out GroundedWheelState groundedWheelState,
            out SlippingWheelState slippingWheelState, out MaterialPhysicsState materialPhysicsState);
        bool parkingBlockState = cc.IsAnyWheelOnParkingBlock();
        bool monorailState = cc.IsCarOnMonorail();
        _isRagdoll |= GetRagdollState(cc);
        RagdollFrameTransform ragdollTransform = GetRagdollFrameTransform(cc);

        _frames.Add(
            new Frame()
            {
                Time = time,
                Speed = speed,
                Position = carTransform.position,
                Rotation = carTransform.rotation.eulerAngles,
                Steering = cc.lerpedSteering,
                ArmsUp = _isArmsUp,
                Braking = _isBraking,
                Horn = _isHorn,
                SoapboxState = cc.currentZeepkistState,
                WheelState = wheelState,
                GroundedWheelState = groundedWheelState,
                SlippingWheelState = slippingWheelState,
                MaterialPhysicsState = materialPhysicsState,
                LocalVelocity = localVelocity,
                LocalAngularVelocity = localAngularVelocity,
                LocalGForce = localGForce,
                ParkingBlockState = parkingBlockState,
                MonorailState = monorailState,
                RagdollState = _isRagdoll,
                RagdollPosition = ragdollTransform.Position,
                RagdollRotation = ragdollTransform.Rotation.eulerAngles,
            });
    }

    private RagdollFrameTransform GetRagdollFrameTransform(New_ControlCar cc)
    {
        DamageCharacterScript characterDamage = _setupCar.characterDamage ?? cc.damageDuge;
        Transform root = characterDamage?.ragdollTransform ?? _setupCar.deadRagdollTop ?? _setupCar.transform;
        if (!_isRagdoll)
            return new RagdollFrameTransform(root.position, root.rotation);

        CacheRagdollComponents(root);

        if (TryGetRagdollRigidbodyFrame(
                root,
                _ragdollRigidbodies,
                out UnityEngine.Vector3 rigidbodyCenter,
                out Quaternion rigidbodyRotation))
            return new RagdollFrameTransform(rigidbodyCenter, rigidbodyRotation);

        if (TryGetRagdollRendererCenter(_ragdollRenderers, out UnityEngine.Vector3 rendererCenter))
            return new RagdollFrameTransform(rendererCenter, root.rotation);

        return new RagdollFrameTransform(root.position, root.rotation);
    }

    private void CacheRagdollComponents(Transform root)
    {
        if (_ragdollRoot == root)
            return;

        _ragdollRoot = root;
        _ragdollRigidbodies = root.GetComponentsInChildren<Rigidbody>(false);
        _ragdollRenderers = root.GetComponentsInChildren<Renderer>(false);
    }

    private static bool TryGetRagdollRigidbodyFrame(
        Transform root,
        Rigidbody[] rigidbodies,
        out UnityEngine.Vector3 center,
        out Quaternion rotation)
    {
        center = UnityEngine.Vector3.zero;
        rotation = root != null ? root.rotation : Quaternion.identity;
        if (root == null)
            return false;

        if (rigidbodies == null || rigidbodies.Length == 0)
            return false;

        int count = 0;
        Rigidbody rotationSource = null;
        foreach (Rigidbody rigidbody in rigidbodies)
        {
            if (rigidbody == null)
                continue;

            center += rigidbody.worldCenterOfMass;
            count++;
            if (rotationSource == null || IsPreferredRagdollRotationSource(rigidbody.transform))
                rotationSource = rigidbody;
        }

        if (count == 0)
            return false;

        center /= count;
        if (rotationSource != null)
            rotation = rotationSource.rotation;
        return true;
    }

    private static bool IsPreferredRagdollRotationSource(Transform transform)
    {
        if (transform == null)
            return false;

        string name = transform.name.ToLowerInvariant();
        return name.Contains("torso") ||
               name.Contains("body") ||
               name.Contains("chest") ||
               name.Contains("spine");
    }

    private static bool TryGetRagdollRendererCenter(Renderer[] renderers, out UnityEngine.Vector3 center)
    {
        center = UnityEngine.Vector3.zero;
        if (renderers == null || renderers.Length == 0)
            return false;

        bool hasBounds = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return false;

        center = bounds.center;
        return true;
    }

    private bool GetRagdollState(New_ControlCar cc)
    {
        if (_setupCar.characterDamage != null && _setupCar.characterDamage.IsDead())
            return true;
        if (cc.GetDead())
            return true;
        return cc.damageDuge != null && cc.damageDuge.IsDead();
    }

    private void SampleWheels(New_ControlCar cc, out WheelState wheels,
        out GroundedWheelState groundedWheels, out SlippingWheelState slippingWheels,
        out MaterialPhysicsState materialPhysics)
    {
        wheels = WheelState.HasNone;
        groundedWheels = GroundedWheelState.HasNone;
        slippingWheels = SlippingWheelState.HasNone;
        materialPhysics = MaterialPhysicsState.None;
        bool soapOverride = cc.currentZeepkistState == 1;
        foreach (New_CustomWheel wheel in cc.wheels)
        {
            bool enabled = wheel.enabled;
            bool grounded = wheel.IsGrounded();
            bool slipping = wheel.IsSlipping();
            switch (wheel.transform.name)
            {
                case "LF":
                    if (enabled) wheels |= WheelState.HasFrontLeft;
                    if (grounded) groundedWheels |= GroundedWheelState.HasFrontLeft;
                    if (slipping) slippingWheels |= SlippingWheelState.HasFrontLeft;
                    break;
                case "RF":
                    if (enabled) wheels |= WheelState.HasFrontRight;
                    if (grounded) groundedWheels |= GroundedWheelState.HasFrontRight;
                    if (slipping) slippingWheels |= SlippingWheelState.HasFrontRight;
                    break;
                case "LR":
                    if (enabled) wheels |= WheelState.HasRearLeft;
                    if (grounded) groundedWheels |= GroundedWheelState.HasRearLeft;
                    if (slipping) slippingWheels |= SlippingWheelState.HasRearLeft;
                    break;
                case "RR":
                    if (enabled) wheels |= WheelState.HasRearRight;
                    if (grounded) groundedWheels |= GroundedWheelState.HasRearRight;
                    if (slipping) slippingWheels |= SlippingWheelState.HasRearRight;
                    break;
            }
            if (MaterialPhysicsStateResolver.ShouldIncludeWheel(enabled, grounded))
            {
                MaterialHolder surface = soapOverride ? null : wheel.GetCurrentSurface();
                MaterialPhysicsState state = MaterialPhysicsStateResolver.GetEffectiveState(soapOverride, surface?.physics?.name);
                materialPhysics |= state;
                LogEncounteredMaterialPhysics(surface, state, soapOverride);
            }
        }
    }

    private void LogEncounteredMaterialPhysics(
        MaterialHolder surfaceMaterial,
        MaterialPhysicsState materialPhysicsState,
        bool soapOverride)
    {
        if (soapOverride)
        {
            if (_loggedSoapSurfaceOverride)
                return;

            _loggedSoapSurfaceOverride = true;
            _logger.LogInformation(
                "Encountered Soap material physics through soapbox state override; " +
                "mapped state: {MaterialPhysicsState}",
                materialPhysicsState);
            return;
        }

        string physicsName = surfaceMaterial?.physics?.name;
        string physicsKey = string.IsNullOrEmpty(physicsName) ? "<null>" : physicsName;
        if (materialPhysicsState == MaterialPhysicsState.None)
        {
            if (!(_unknownMaterialPhysicsNames ??= new HashSet<string>(StringComparer.Ordinal)).Add(physicsKey))
                return;

            _logger.LogWarning(
                "Encountered unmapped material physics {MaterialPhysicsName}; " +
                "surface material: {SurfaceMaterialName} ({SurfaceMaterialId}, {LocalizedSurfaceName}); " +
                "physic material: {PhysicMaterialName}; " +
                "particle type: {SurfaceParticleType} ({SurfaceParticleTypeValue})",
                physicsKey,
                surfaceMaterial?.name ?? "<null>",
                surfaceMaterial?.materialID ?? 0,
                surfaceMaterial?.localizedName ?? "<none>",
                surfaceMaterial?.physicMaterial?.name ?? "<null>",
                surfaceMaterial?.particle.ToString() ?? "<null>",
                surfaceMaterial == null ? -1 : (int)surfaceMaterial.particle);
            return;
        }

        if (!(_encounteredMaterialPhysicsNames ??= new HashSet<string>(StringComparer.Ordinal)).Add(physicsName))
            return;

        _logger.LogInformation(
            "Encountered material physics {MaterialPhysicsName}; " +
            "surface material: {SurfaceMaterialName} ({SurfaceMaterialId}, {LocalizedSurfaceName}); " +
            "physic material: {PhysicMaterialName}; " +
            "particle type: {SurfaceParticleType} ({SurfaceParticleTypeValue}); " +
            "mapped state: {MaterialPhysicsState}",
            physicsName,
            surfaceMaterial?.name ?? "<null>",
            surfaceMaterial?.materialID ?? 0,
            surfaceMaterial?.localizedName ?? "<none>",
            surfaceMaterial?.physicMaterial?.name ?? "<null>",
            surfaceMaterial?.particle.ToString() ?? "<null>",
            surfaceMaterial == null ? -1 : (int)surfaceMaterial.particle,
            materialPhysicsState);
    }

    private static WheelState GetWheelState(New_ControlCar cc)
    {
        WheelState wheelState = WheelState.HasNone;

        foreach (New_CustomWheel wheel in cc.wheels)
        {
            string name = wheel.transform.name;
            switch (name)
            {
                case "LF":
                    if (wheel.enabled)
                        wheelState |= WheelState.HasFrontLeft;
                    break;
                case "RF":
                    if (wheel.enabled)
                        wheelState |= WheelState.HasFrontRight;
                    break;
                case "LR":
                    if (wheel.enabled)
                        wheelState |= WheelState.HasRearLeft;
                    break;
                case "RR":
                    if (wheel.enabled)
                        wheelState |= WheelState.HasRearRight;
                    break;
            }
        }

        return wheelState;
    }

    private static GroundedWheelState GetGroundedWheelState(New_ControlCar cc)
    {
        GroundedWheelState groundedWheelState = GroundedWheelState.HasNone;

        foreach (New_CustomWheel wheel in cc.wheels)
        {
            string name = wheel.transform.name;
            switch (name)
            {
                case "LF":
                    if (wheel.IsGrounded())
                        groundedWheelState |= GroundedWheelState.HasFrontLeft;
                    break;
                case "RF":
                    if (wheel.IsGrounded())
                        groundedWheelState |= GroundedWheelState.HasFrontRight;
                    break;
                case "LR":
                    if (wheel.IsGrounded())
                        groundedWheelState |= GroundedWheelState.HasRearLeft;
                    break;
                case "RR":
                    if (wheel.IsGrounded())
                        groundedWheelState |= GroundedWheelState.HasRearRight;
                    break;
            }
        }

        return groundedWheelState;
    }

    private static SlippingWheelState GetSlippingWheelState(New_ControlCar cc)
    {
        SlippingWheelState slippingWheelState = SlippingWheelState.HasNone;

        foreach (New_CustomWheel wheel in cc.wheels)
        {
            string name = wheel.transform.name;
            switch (name)
            {
                case "LF":
                    if (wheel.IsSlipping())
                        slippingWheelState |= SlippingWheelState.HasFrontLeft;
                    break;
                case "RF":
                    if (wheel.IsSlipping())
                        slippingWheelState |= SlippingWheelState.HasFrontRight;
                    break;
                case "LR":
                    if (wheel.IsSlipping())
                        slippingWheelState |= SlippingWheelState.HasRearLeft;
                    break;
                case "RR":
                    if (wheel.IsSlipping())
                        slippingWheelState |= SlippingWheelState.HasRearRight;
                    break;
            }
        }

        return slippingWheelState;
    }

    public async UniTask<bool> Write(Stream stream)
    {
        try
        {
            using Snapshot snapshot = Freeze();
            await Task.Run(() => snapshot.Write(stream));
            return true;
        }
        catch (Exception error)
        {
            _logger.LogError(error, "Error while serializing/encoding ghost");
            return false;
        }
    }

    internal Snapshot Freeze()
    {
        if (_sealed)
            throw new InvalidOperationException("Recording was already detached.");
        GameSettingsScriptableObject gameSettings = PlayerManager.Instance.instellingen.GlobalSettings;

        Ghost ghost = new()
        {
            Version = 7,
            SteamId = SteamClient.SteamId.Value,
            TaggedUsername = PlayerManager.Instance.GetNameTag() + SteamClient.Name,
            Color = ColorUtilities.ToHexString(
                Color.HSVToRGB(
                    gameSettings.online_name_color_H,
                    gameSettings.online_name_color_S,
                    gameSettings.online_name_color_V))
        };

        CosmeticIDs ids = PlayerManager.Instance.adventureCosmetics.GetIDs();

        ghost.Cosmetics = new Cosmetics()
        {
            Color = ids.color,
            ColorBody = ids.color_body,
            ColorLeftArm = ids.color_leftArm,
            ColorLeftLeg = ids.color_leftLeg,
            ColorRightArm = ids.color_rightArm,
            ColorRightLeg = ids.color_rightLeg,
            FrontWheels = ids.frontWheels,
            Glasses = ids.glasses,
            Hat = ids.hat,
            Horn = ids.horn,
            Paraglider = ids.paraglider,
            RearWheels = ids.rearWheels,
            Zeepkist = ids.zeepkist,
        };

        Stop();
        _sealed = true;
        var snapshot = new Snapshot(ghost, _frames);
        _frames = new StructFrameBuffer<Frame>(FrameBlockSize);
        _setupCar = null;
        _readyToReset = null;
        _ragdollRoot = null;
        _ragdollRigidbodies = null;
        _ragdollRenderers = null;
        return snapshot;
    }

}
