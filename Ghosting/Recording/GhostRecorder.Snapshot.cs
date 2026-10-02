using System;
using System.Collections.Generic;
using System.IO;
using EasyCompressor;
using ProtoBuf;
using TNRD.Zeepkist.GTR.Ghosting.Recording.Data;
using UnityEngine;
using Vector2Int = TNRD.Zeepkist.GTR.Ghosting.Recording.Data.Vector2Int;
using Vector3 = TNRD.Zeepkist.GTR.Ghosting.Recording.Data.Vector3;
using Vector3Int = TNRD.Zeepkist.GTR.Ghosting.Recording.Data.Vector3Int;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public partial class GhostRecorder
{
    private const int PositionMultiplier = 100_000;
    private const int RotationMultiplier = 100;

    internal sealed class Snapshot : IDisposable
    {
        private readonly Ghost _header;
        private IReadOnlyCollection<Frame> _frames;
        private bool _written;

        internal Snapshot(Ghost header, IReadOnlyCollection<Frame> frames)
        {
            _header = header;
            _frames = frames;
        }

        public void Write(Stream stream)
        {
            if (_frames == null)
                throw new ObjectDisposedException(nameof(Snapshot));
            if (_written)
                throw new InvalidOperationException("Recording was already encoded.");
            _written = true;
            Ghost ghost = CreateGhost(_header, _frames);
            using MemoryStream payload = new();
            Serializer.Serialize(payload, ghost);
            payload.Position = 0;
            Encode(payload, stream);
        }

        public void Dispose()
        {
            _frames = null;
            _header.DeltaFrames = null;
        }
    }

    private static Ghost CreateGhost(Ghost ghost, IReadOnlyCollection<Frame> frames)
    {
        int capacity = Math.Max(0, frames.Count - 1);
        List<DeltaFrame> deltaFrames = new(capacity);

        Frame previousFrame = default;
        foreach (Frame frame in frames)
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
                    frame.MaterialPhysicsState,
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
                UnityEngine.Vector3 encodedRagdollPosition = !previousFrame.RagdollState
                    ? frame.RagdollPosition
                    : frame.RagdollPosition - previousFrame.RagdollPosition;
                UnityEngine.Vector3 encodedRagdollRotation = !previousFrame.RagdollState
                    ? frame.RagdollRotation
                    : frame.RagdollRotation - previousFrame.RagdollRotation;
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
                    frame.MaterialPhysicsState,
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
        var compressor = new LZMACompressor { CompressionLevel = LZMACompressionLevel.Ultra };
        compressor.Compress(inputStream, outStream);
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
        if ((frame.WheelState & WheelState.HasFrontLeft) != 0)
            soapboxFlags |= SoapboxFlags.FrontLeft;
        if ((frame.WheelState & WheelState.HasFrontRight) != 0)
            soapboxFlags |= SoapboxFlags.FrontRight;
        if ((frame.WheelState & WheelState.HasRearLeft) != 0)
            soapboxFlags |= SoapboxFlags.RearLeft;
        if ((frame.WheelState & WheelState.HasRearRight) != 0)
            soapboxFlags |= SoapboxFlags.RearRight;

        return soapboxFlags;
    }
}
