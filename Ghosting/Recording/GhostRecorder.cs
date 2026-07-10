using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EasyCompressor;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using Steamworks;
using TNRD.Zeepkist.GTR.Ghosting.Recording.Data;
using TNRD.Zeepkist.GTR.PlayerLoop;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;
using ZeepkistNetworking;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using Vector2Int = TNRD.Zeepkist.GTR.Ghosting.Recording.Data.Vector2Int;
using Vector3 = TNRD.Zeepkist.GTR.Ghosting.Recording.Data.Vector3;
using Vector3Int = TNRD.Zeepkist.GTR.Ghosting.Recording.Data.Vector3Int;

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

    private const int PositionMultiplier = 100_000;
    private const int RotationMultiplier = 100;
    private const int InitialFrameCapacity = 4_096;

    private readonly PlayerLoopService _playerLoopService;
    private readonly List<Frame> _frames = new(InitialFrameCapacity);
    private readonly ILogger<GhostRecorder> _logger;

    private PlayerLoopSubscription _updateToken;
    private PlayerLoopSubscription _fixedUpdateToken;
    private SetupCar _setupCar;
    private ReadyToReset _readyToReset;

    private bool _isBraking;
    private bool _isHorn;
    private bool _isArmsUp;
    private bool _isRagdoll;

    public GhostRecorder(PlayerLoopService playerLoopService, ILogger<GhostRecorder> logger)
    {
        _playerLoopService = playerLoopService;
        _logger = logger;
    }

    public void Start()
    {
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
        WheelState wheelState = GetWheelState(cc);
        GroundedWheelState groundedWheelState = GetGroundedWheelState(cc);
        SlippingWheelState slippingWheelState = GetSlippingWheelState(cc);
        SurfaceState surfaceState = GetSurfaceState(cc);
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
                SurfaceState = surfaceState,
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

        if (TryGetRagdollRigidbodyFrame(root, out UnityEngine.Vector3 rigidbodyCenter, out Quaternion rigidbodyRotation))
            return new RagdollFrameTransform(rigidbodyCenter, rigidbodyRotation);

        if (TryGetRagdollRendererCenter(root, out UnityEngine.Vector3 rendererCenter))
            return new RagdollFrameTransform(rendererCenter, root.rotation);

        return new RagdollFrameTransform(root.position, root.rotation);
    }

    private static bool TryGetRagdollRigidbodyFrame(
        Transform root,
        out UnityEngine.Vector3 center,
        out Quaternion rotation)
    {
        center = UnityEngine.Vector3.zero;
        rotation = root != null ? root.rotation : Quaternion.identity;
        if (root == null)
            return false;

        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(false);
        if (rigidbodies.Length == 0)
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

    private static bool TryGetRagdollRendererCenter(Transform root, out UnityEngine.Vector3 center)
    {
        center = UnityEngine.Vector3.zero;
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length == 0)
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

    private static SurfaceState GetSurfaceState(New_ControlCar cc)
    {
        SurfaceState surfaceState = SurfaceState.None;
        foreach (var surfaceAndSlippin in cc.GetSlipAndSurfaceList())
        {
            surfaceState |= GetSurfaceState(surfaceAndSlippin.whichSurface);
        }

        return surfaceState == SurfaceState.None ? SurfaceState.Tarmac : surfaceState;
    }

    private static SurfaceState GetSurfaceState(object surface)
    {
        if (surface == null)
            return SurfaceState.Tarmac;

        string name;

        if (surface is UnityEngine.Object unityObject)
            name = unityObject.name;
        else
            name = surface.ToString();

        return SurfaceKeyNormalizer.NormalizeSurfaceKey(name) switch
        {
            "grass" => SurfaceState.Grass,
            "sand" => SurfaceState.Sand,
            "snow" => SurfaceState.Snow,
            "ice" => SurfaceState.Ice,
            "soap" => SurfaceState.Soap,
            "metal" => SurfaceState.Metal,
            _ => SurfaceState.Tarmac
        };
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
        Ghost ghost;

        try
        {
            ghost = await CreateGhost();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while creating ghost");
            return false;
        }

        try
        {
            await Task.Run(() =>
            {
                using MemoryStream memoryStream = new();
                Serializer.Serialize(memoryStream, ghost);
                memoryStream.Position = 0;
                Encode(memoryStream, stream);
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while serializing/encoding ghost");
            return false;
        }

        return true;
    }

    private async UniTask<Ghost> CreateGhost()
    {
        GameSettingsScriptableObject gameSettings = PlayerManager.Instance.instellingen.GlobalSettings;

        Ghost ghost = new();
        ghost.Version = 6;
        ghost.SteamId = SteamClient.SteamId.Value;
        ghost.TaggedUsername = PlayerManager.Instance.GetNameTag() + SteamClient.Name;
        ghost.Color = ColorUtilities.ToHexString(
            Color.HSVToRGB(
                gameSettings.online_name_color_H,
                gameSettings.online_name_color_S,
                gameSettings.online_name_color_V));
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

        List<DeltaFrame> deltaFrames = await Task.Run(() =>
        {
            int capacity = Math.Max(0, _frames.Count - 1);
            List<DeltaFrame> deltaFrames = new(capacity);

            Frame previousFrame = null;
            foreach (Frame frame in _frames)
            {
                if (ghost.InitialFrame == null)
                {
                    ghost.InitialFrame = new InitialFrame(
                        new Vector3(frame.Position.x, frame.Position.y, frame.Position.z),
                        new Vector3(frame.Rotation.x, frame.Rotation.y, frame.Rotation.z),
                        ClampToByte(frame.Speed),
                        RemapToByte(frame.Steering, -1, 1),
                        (Data.InputFlags)(byte)CreateInputFlags(frame),
                        (Data.SoapboxFlags)(byte)CreateSoapboxFlags(frame),
                        frame.GroundedWheelState,
                        frame.SlippingWheelState,
                        frame.SurfaceState,
                        ToScaledVector3Int(frame.LocalVelocity, PositionMultiplier),
                        ToScaledVector3Int(frame.LocalAngularVelocity, RotationMultiplier),
                        ToScaledVector2Int(frame.LocalGForce, PositionMultiplier),
                        frame.ParkingBlockState,
                        frame.MonorailState,
                        frame.RagdollState,
                        frame.RagdollState ? ToScaledVector3Int(frame.RagdollPosition, PositionMultiplier) : new Vector3Int(),
                        frame.RagdollState ? ToScaledVector3Int(frame.RagdollRotation, RotationMultiplier) : new Vector3Int());
                }
                else
                {
                    UnityEngine.Vector3 deltaPosition = frame.Position - previousFrame.Position;
                    Frame previousRagdollFrame = previousFrame.RagdollState ? previousFrame : null;
                    UnityEngine.Vector3 encodedRagdollPosition = previousRagdollFrame == null
                        ? frame.RagdollPosition
                        : frame.RagdollPosition - previousRagdollFrame.RagdollPosition;
                    UnityEngine.Vector3 encodedRagdollRotation = previousRagdollFrame == null
                        ? frame.RagdollRotation
                        : frame.RagdollRotation - previousRagdollFrame.RagdollRotation;
                    DeltaFrame deltaFrame = new(
                        frame.Time,
                        ToScaledVector3Int(deltaPosition, PositionMultiplier),
                        ToScaledVector3Int(frame.Rotation, RotationMultiplier),
                        ClampToByte(frame.Speed),
                        RemapToByte(frame.Steering, -1, 1),
                        (Data.InputFlags)(byte)CreateInputFlags(frame),
                        (Data.SoapboxFlags)(byte)CreateSoapboxFlags(frame),
                        frame.GroundedWheelState,
                        frame.SlippingWheelState,
                        frame.SurfaceState,
                        ToScaledVector3Int(frame.LocalVelocity, PositionMultiplier),
                        ToScaledVector3Int(frame.LocalAngularVelocity, RotationMultiplier),
                        ToScaledVector2Int(frame.LocalGForce, PositionMultiplier),
                        frame.ParkingBlockState,
                        frame.MonorailState,
                        frame.RagdollState,
                        frame.RagdollState ? ToScaledVector3Int(encodedRagdollPosition, PositionMultiplier) : new Vector3Int(),
                        frame.RagdollState ? ToScaledVector3Int(encodedRagdollRotation, RotationMultiplier) : new Vector3Int());
                    deltaFrames.Add(deltaFrame);
                }

                previousFrame = frame;
            }

            return deltaFrames;
        });

        ghost.DeltaFrames = deltaFrames;
        return ghost;
    }

    private static byte RemapToByte(float input, float min, float max)
    {
        return ClampToByte(Mathf.InverseLerp(min, max, input) * 255);
    }

    private static byte ClampToByte(float value)
    {
        return (byte)Mathf.Clamp(value, 0, 255);
    }

    private static Vector3Int ToScaledVector3Int(UnityEngine.Vector3 value, int multiplier)
    {
        return new Vector3Int(
            Mathf.RoundToInt(value.x * multiplier),
            Mathf.RoundToInt(value.y * multiplier),
            Mathf.RoundToInt(value.z * multiplier));
    }

    private static Vector2Int ToScaledVector2Int(UnityEngine.Vector2 value, int multiplier)
    {
        return new Vector2Int(
            Mathf.RoundToInt(value.x * multiplier),
            Mathf.RoundToInt(value.y * multiplier));
    }

    private static void Encode(Stream inputStream, Stream outStream)
    {
        LZMACompressor.Shared.CompressionLevel = LZMACompressionLevel.Ultra;
        LZMACompressor.Shared.Compress(inputStream, outStream);
    }

    private static InputFlags CreateInputFlags(Frame frame)
    {
        InputFlags inputFlags = InputFlags.None;

        if (frame.ArmsUp)
            inputFlags |= InputFlags.ArmsUp;
        if (frame.Braking)
            inputFlags |= InputFlags.Braking;
        if (frame.Horn)
            inputFlags |= InputFlags.Horn;

        return inputFlags;
    }

    private static SoapboxFlags CreateSoapboxFlags(Frame frame)
    {
        SoapboxFlags soapboxFlags = SoapboxFlags.None;

        if (frame.SoapboxState == 1)
            soapboxFlags |= SoapboxFlags.Soap;
        if (frame.SoapboxState == 2)
            soapboxFlags |= SoapboxFlags.Offroad;
        if (frame.SoapboxState == 3)
            soapboxFlags |= SoapboxFlags.Paraglider;
        if (frame.WheelState.HasFlag(WheelState.HasFrontLeft))
            soapboxFlags |= SoapboxFlags.FrontLeft;
        if (frame.WheelState.HasFlag(WheelState.HasFrontRight))
            soapboxFlags |= SoapboxFlags.FrontRight;
        if (frame.WheelState.HasFlag(WheelState.HasRearLeft))
            soapboxFlags |= SoapboxFlags.RearLeft;
        if (frame.WheelState.HasFlag(WheelState.HasRearRight))
            soapboxFlags |= SoapboxFlags.RearRight;

        return soapboxFlags;
    }
}
