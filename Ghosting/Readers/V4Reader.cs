using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

public class V4Reader : IGhostReader
{
    public IGhost Read(byte[] data)
    {
        List<V4Ghost.Frame> frames = new();
        ulong steamId;
        int soapboxId;
        int hatId;
        int colorId;

        using MemoryStream ms = new(data);
        GZipStream zip = null;
        BinaryReader reader = null;

        if (IsGZipped(data))
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
            reader.ReadInt32();
            steamId = reader.ReadUInt64();
            soapboxId = reader.ReadInt32();
            hatId = reader.ReadInt32();
            colorId = reader.ReadInt32();
            byte precision = reader.ReadByte();
            int frameCount = reader.ReadInt32();

            ResetFrame lastResetFrame = null;
            List<DeltaFrame> deltaFrames = new();

            for (int i = 0; i < frameCount; i++)
            {
                if (i % precision == 0 || i == frameCount - 1)
                {
                    (V4Ghost.Frame frame, ResetFrame resetFrame) = ReadResetFrame(reader, deltaFrames);
                    frames.Add(frame);
                    lastResetFrame = resetFrame;
                }
                else
                {
                    List<V4Ghost.Frame> parsedFrames = ReadDeltaFrame(reader, deltaFrames, lastResetFrame);
                    frames.AddRange(parsedFrames);
                }
            }
        }
        finally
        {
            reader.Dispose();
            zip?.Dispose();
        }

        return new V4Ghost(steamId, soapboxId, hatId, colorId, frames);
    }

    private static (V4Ghost.Frame, ResetFrame) ReadResetFrame(BinaryReader reader, List<DeltaFrame> deltaFrames)
    {
        ResetFrame lastResetFrame = ResetFrame.Read(reader);
        deltaFrames.Clear();

        return (new V4Ghost.Frame(
                lastResetFrame.Time,
                new Vector3(
                    lastResetFrame.PositionX,
                    lastResetFrame.PositionY,
                    lastResetFrame.PositionZ),
                new Quaternion(
                    ShortToFloat(lastResetFrame.RotationX),
                    ShortToFloat(lastResetFrame.RotationY),
                    ShortToFloat(lastResetFrame.RotationZ),
                    ShortToFloat(lastResetFrame.RotationW)),
                lastResetFrame.Steering,
                lastResetFrame.Flags.HasFlag(Flags.ArmsUp),
                lastResetFrame.Flags.HasFlag(Flags.IsBraking)),
            lastResetFrame);
    }

    private static List<V4Ghost.Frame> ReadDeltaFrame(
        BinaryReader reader,
        List<DeltaFrame> deltaFrames,
        ResetFrame lastResetFrame)
    {
        DeltaFrame deltaFrame = DeltaFrame.Read(reader);
        deltaFrames.Add(deltaFrame);

        List<V4Ghost.Frame> frames = new();

        Vector3 position = new Vector3(
            lastResetFrame.PositionX,
            lastResetFrame.PositionY,
            lastResetFrame.PositionZ);

        foreach (DeltaFrame frame in deltaFrames)
        {
            position += new Vector3(
                ShortToFloat(frame.PositionX),
                ShortToFloat(frame.PositionY),
                ShortToFloat(frame.PositionZ));

            Vector4 rotation = new(
                ShortToFloat(frame.RotationX, 30000),
                ShortToFloat(frame.RotationY, 30000),
                ShortToFloat(frame.RotationZ, 30000),
                ShortToFloat(frame.RotationW, 30000));

            frames.Add(
                new V4Ghost.Frame(
                    frame.Time,
                    position,
                    new Quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                    frame.Steering,
                    frame.Flags.HasFlag(Flags.ArmsUp),
                    frame.Flags.HasFlag(Flags.IsBraking)));
        }

        return frames;
    }

    private static bool IsGZipped(byte[] buffer)
    {
        return buffer[0] == 0x1f && buffer[1] == 0x8b;
    }

    private static float ShortToFloat(short value, float scale = 10000f)
    {
        return value / scale;
    }

    public abstract class GhostFrame
    {
        public float Time;
    }

    public class DeltaFrame : GhostFrame
    {
        public short PositionX;
        public short PositionY;
        public short PositionZ;
        public short RotationX;
        public short RotationY;
        public short RotationZ;
        public short RotationW;
        public byte Steering;
        public Flags Flags;

        public static DeltaFrame Read(BinaryReader reader)
        {
            DeltaFrame f = new()
            {
                Time = reader.ReadSingle(),
                PositionX = reader.ReadInt16(),
                PositionY = reader.ReadInt16(),
                PositionZ = reader.ReadInt16(),
                RotationX = reader.ReadInt16(),
                RotationY = reader.ReadInt16(),
                RotationZ = reader.ReadInt16(),
                RotationW = reader.ReadInt16(),
                Steering = reader.ReadByte(),
                Flags = (Flags)reader.ReadByte()
            };
            return f;
        }
    }

    public class ResetFrame : GhostFrame
    {
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public short RotationX;
        public short RotationY;
        public short RotationZ;
        public short RotationW;
        public byte Steering;
        public Flags Flags;

        public static ResetFrame Read(BinaryReader reader)
        {
            ResetFrame f = new();
            f.Time = reader.ReadSingle();
            f.PositionX = reader.ReadSingle();
            f.PositionY = reader.ReadSingle();
            f.PositionZ = reader.ReadSingle();
            f.RotationX = reader.ReadInt16();
            f.RotationY = reader.ReadInt16();
            f.RotationZ = reader.ReadInt16();
            f.RotationW = reader.ReadInt16();
            f.Steering = reader.ReadByte();
            f.Flags = (Flags)reader.ReadByte();
            return f;
        }
    }

    [Flags]
    public enum Flags : byte
    {
        None = 0,
        ArmsUp = 1 << 0,
        IsBraking = 1 << 1,
    }
}
