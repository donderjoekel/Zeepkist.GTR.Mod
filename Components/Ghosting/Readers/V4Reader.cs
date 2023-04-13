using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public class V4Reader : IGhostReader
{
    [Flags]
    public enum Flags : byte
    {
        None = 0,
        ArmsUp = 1 << 0,
        IsBraking = 1 << 1,
    }

    public abstract class Frame
    {
        public float time;
    }

    public class ResetFrame : Frame
    {
        public float positionX;
        public float positionY;
        public float positionZ;
        public short rotationX;
        public short rotationY;
        public short rotationZ;
        public short rotationW;
        public byte steering;
        public Flags flags;

        public static ResetFrame Read(BinaryReader reader)
        {
            ResetFrame f = new ResetFrame();
            f.time = reader.ReadSingle();
            f.positionX = reader.ReadSingle();
            f.positionY = reader.ReadSingle();
            f.positionZ = reader.ReadSingle();
            f.rotationX = reader.ReadInt16();
            f.rotationY = reader.ReadInt16();
            f.rotationZ = reader.ReadInt16();
            f.rotationW = reader.ReadInt16();
            f.steering = reader.ReadByte();
            f.flags = (Flags)reader.ReadByte();
            return f;
        }
    }

    public class DeltaFrame : Frame
    {
        public short positionX;
        public short positionY;
        public short positionZ;
        public short rotationX;
        public short rotationY;
        public short rotationZ;
        public short rotationW;
        public byte steering;
        public Flags flags;

        public static DeltaFrame Read(BinaryReader reader)
        {
            DeltaFrame f = new DeltaFrame();
            f.time = reader.ReadSingle();
            f.positionX = reader.ReadInt16();
            f.positionY = reader.ReadInt16();
            f.positionZ = reader.ReadInt16();
            f.rotationX = reader.ReadInt16();
            f.rotationY = reader.ReadInt16();
            f.rotationZ = reader.ReadInt16();
            f.rotationW = reader.ReadInt16();
            f.steering = reader.ReadByte();
            f.flags = (Flags)reader.ReadByte();
            return f;
        }
    }

    private readonly List<FrameDataWithTime> frames = new List<FrameDataWithTime>();
    private byte precision;

    /// <inheritdoc />
    public int Version => 4;

    /// <inheritdoc />
    public ulong SteamId { get; private set; }

    /// <inheritdoc />
    public int SoapboxId { get; private set; }

    /// <inheritdoc />
    public int HatId { get; private set; }

    /// <inheritdoc />
    public int ColorId { get; private set; }

    /// <inheritdoc />
    public void Read(byte[] buffer)
    {
        frames.Clear();

        using MemoryStream ms = new MemoryStream(buffer);

        GZipStream zip = null;
        BinaryReader reader = null;

        if (IsGZipped(buffer))
        {
            zip = new GZipStream(ms, CompressionMode.Decompress);
            reader = new BinaryReader(zip);
        }
        else
        {
            reader = new BinaryReader(ms);
        }

        try
        {
            int version = reader.ReadInt32();
            SteamId = reader.ReadUInt64();
            SoapboxId = reader.ReadInt32();
            HatId = reader.ReadInt32();
            ColorId = reader.ReadInt32();
            precision = reader.ReadByte();
            int frameCount = reader.ReadInt32();

            ResetFrame lastResetFrame = null;
            List<DeltaFrame> deltaFrames = new List<DeltaFrame>();

            for (int i = 0; i < frameCount; i++)
            {
                if (i % precision == 0 || i == frameCount - 1)
                {
                    lastResetFrame = ResetFrame.Read(reader);
                    deltaFrames.Clear();

                    frames.Add(new FrameDataWithTime()
                    {
                        Position = new Vector3(
                            lastResetFrame.positionX,
                            lastResetFrame.positionY,
                            lastResetFrame.positionZ),
                        Rotation = new Quaternion(
                            ShortToFloat(lastResetFrame.rotationX, 30000),
                            ShortToFloat(lastResetFrame.rotationY, 30000),
                            ShortToFloat(lastResetFrame.rotationZ, 30000),
                            ShortToFloat(lastResetFrame.rotationW, 30000)),
                        Steering = ByteToFloat(lastResetFrame.steering),
                        Time = lastResetFrame.time,
                        ArmsUp = (lastResetFrame.flags & Flags.ArmsUp) != 0,
                        IsBraking = (lastResetFrame.flags & Flags.IsBraking) != 0
                    });
                }
                else
                {
                    DeltaFrame deltaFrame = DeltaFrame.Read(reader);
                    deltaFrames.Add(deltaFrame);

                    Vector3 position = new Vector3(
                        lastResetFrame.positionX,
                        lastResetFrame.positionY,
                        lastResetFrame.positionZ);

                    Vector4 rotation = new Vector4(
                        ShortToFloat(lastResetFrame.rotationX),
                        ShortToFloat(lastResetFrame.rotationY),
                        ShortToFloat(lastResetFrame.rotationZ),
                        ShortToFloat(lastResetFrame.rotationW));

                    foreach (DeltaFrame frame in deltaFrames)
                    {
                        position += new Vector3(
                            ShortToFloat(frame.positionX),
                            ShortToFloat(frame.positionY),
                            ShortToFloat(frame.positionZ));

                        rotation = new Vector4(
                            ShortToFloat(frame.rotationX, 30000),
                            ShortToFloat(frame.rotationY, 30000),
                            ShortToFloat(frame.rotationZ, 30000),
                            ShortToFloat(frame.rotationW, 30000));
                    }

                    frames.Add(new FrameDataWithTime()
                    {
                        Position = position,
                        Rotation = new Quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                        Steering = ByteToFloat(deltaFrame.steering),
                        Time = deltaFrame.time,
                        ArmsUp = (deltaFrame.flags & Flags.ArmsUp) != 0,
                        IsBraking = (deltaFrame.flags & Flags.IsBraking) != 0
                    });
                }
            }
        }
        finally
        {
            zip?.Dispose();
            reader.Dispose();
        }
    }

    private static bool IsGZipped(byte[] buffer)
    {
        return buffer[0] == 0x1f && buffer[1] == 0x8b;
    }

    /// <inheritdoc />
    public FrameData GetFrameData(float time)
    {
        FrameDataWithTime startFrame = null;
        FrameDataWithTime endFrame = null;

        foreach (FrameDataWithTime f in frames)
        {
            if (f.Time < time)
            {
                startFrame = f;
            }
            else
            {
                endFrame = f;
                break;
            }
        }

        if (startFrame == null && endFrame != null)
        {
            return endFrame;
        }

        if (startFrame != null && endFrame == null)
        {
            return startFrame;
        }

        float inverseLerpedTime = Mathf.InverseLerp(startFrame.Time, endFrame.Time, time);

        return new FrameData()
        {
            Position = Vector3.LerpUnclamped(startFrame.Position, endFrame.Position, inverseLerpedTime),
            Rotation = Quaternion.LerpUnclamped(startFrame.Rotation, endFrame.Rotation, inverseLerpedTime),
            Steering = Mathf.LerpUnclamped(startFrame.Steering, endFrame.Steering, inverseLerpedTime),
            IsBraking = endFrame.IsBraking,
            ArmsUp = endFrame.ArmsUp
        };
    }

    private static float ShortToFloat(short value, float scale = 10000f)
    {
        return value / scale;
    }

    private float ByteToFloat(byte value)
    {
        return (value / 127.5f) - 1;
    }
}
