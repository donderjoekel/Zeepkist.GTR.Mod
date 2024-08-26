using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EasyCompressor;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using SevenZip.Compression.LZMA;
using Steamworks;
using TNRD.Zeepkist.GTR.Ghosting.Recording.Data;
using TNRD.Zeepkist.GTR.PlayerLoop;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;
using ZeepkistNetworking;
using Vector3 = TNRD.Zeepkist.GTR.Ghosting.Recording.Data.Vector3;
using Vector3Int = TNRD.Zeepkist.GTR.Ghosting.Recording.Data.Vector3Int;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public partial class GhostRecorder
{
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

        Transform carTransform = _setupCar.transform;
        New_ControlCar cc = _setupCar.cc;

        _frames.Add(
            new Frame()
            {
                Time = _readyToReset.ticker.what_ticker,
                Speed = cc.GetLocalVelocity().magnitude * 3.6f,
                Position = carTransform.position,
                Rotation = carTransform.rotation.eulerAngles,
                Steering = cc.lerpedSteering,
                ArmsUp = _isArmsUp,
                Braking = _isBraking,
                Horn = _isHorn,
                SoapboxState = cc.currentZeepkistState,
                WheelState = GetWheelState(cc),
            });
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

    public bool Write(Stream stream)
    {
        Ghost ghost;

        try
        {
            ghost = CreateGhost();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while creating ghost");
            return false;
        }

        try
        {
            using MemoryStream memoryStream = new();
            Serializer.Serialize(memoryStream, ghost);
            memoryStream.Close();
            byte[] buffer = memoryStream.ToArray();
            Encode(buffer, stream);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while serializing/encoding ghost");
            return false;
        }

        return true;
    }

    private Ghost CreateGhost()
    {
        GameSettingsScriptableObject gameSettings = PlayerManager.Instance.instellingen.GlobalSettings;

        Ghost ghost = new();
        ghost.Version = 5;
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
                    (Data.SoapboxFlags)(byte)CreateSoapboxFlags(frame));
            }
            else
            {
                const int positionMultiplier = 100_000;
                const int rotationMultiplier = 100;
                UnityEngine.Vector3 deltaPosition = frame.Position - previousFrame.Position;
                DeltaFrame deltaFrame = new(
                    frame.Time,
                    new Vector3Int(
                        Mathf.RoundToInt(deltaPosition.x * positionMultiplier),
                        Mathf.RoundToInt(deltaPosition.y * positionMultiplier),
                        Mathf.RoundToInt(deltaPosition.z * positionMultiplier)),
                    new Vector3Int(
                        Mathf.RoundToInt(frame.Rotation.x * rotationMultiplier),
                        Mathf.RoundToInt(frame.Rotation.y * rotationMultiplier),
                        Mathf.RoundToInt(frame.Rotation.z * rotationMultiplier)),
                    ClampToByte(frame.Speed),
                    RemapToByte(frame.Steering, -1, 1),
                    (Data.InputFlags)(byte)CreateInputFlags(frame),
                    (Data.SoapboxFlags)(byte)CreateSoapboxFlags(frame));
                deltaFrames.Add(deltaFrame);
            }

            previousFrame = frame;
        }

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

    private static void Encode(byte[] buffer, Stream outStream)
    {
        LZMACompressor.Shared.CompressionLevel = LZMACompressionLevel.Ultra;
        LZMACompressor.Shared.Compress(buffer, outStream);
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
