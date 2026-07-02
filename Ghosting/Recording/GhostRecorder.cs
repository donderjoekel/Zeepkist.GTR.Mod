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
using Vector3 = TNRD.Zeepkist.GTR.Ghosting.Recording.Data.Vector3;
using Vector2Int = TNRD.Zeepkist.GTR.Ghosting.Recording.Data.Vector2Int;
using Vector3Int = TNRD.Zeepkist.GTR.Ghosting.Recording.Data.Vector3Int;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public partial class GhostRecorder
{
    private const int PositionMultiplier = 100_000;
    private const int RotationMultiplier = 100;

    private readonly PlayerLoopService _playerLoopService;
    private readonly List<Frame> _frames = new();
    private readonly ILogger<GhostRecorder> _logger;

    private PlayerLoopSubscription _updateToken;
    private PlayerLoopSubscription _fixedUpdateToken;
    private SetupCar _setupCar;
    private ReadyToReset _readyToReset;

    private bool _isBraking;
    private bool _isHorn;
    private bool _isArmsUp;

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
        if (_frames.Count >= GhostLimits.MaxFrames)
        {
            _logger.LogWarning("Ghost frame limit reached; stopping recording");
            Stop();
            return;
        }

        Transform carTransform = _setupCar.transform;
        New_ControlCar cc = _setupCar.cc;
        float time = _readyToReset.ticker.what_ticker;
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
            });
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
        ghost.Statistics = CalculateStatistics(_frames);
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
            List<DeltaFrame> deltaFrames = new();

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
                        frame.MonorailState);
                }
                else
                {
                    UnityEngine.Vector3 deltaPosition = frame.Position - previousFrame.Position;
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
                        frame.MonorailState);
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
