using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public class V3Reader : IGhostReader
{
    private class Frame
    {
        public float Time { get; private set; }
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public float Steering { get; private set; }
        public bool ArmsUp { get; private set; }
        public bool IsBraking { get; private set; }

        public static Frame Read(BinaryReader reader)
        {
            Frame f = new Frame();
            f.Time = reader.ReadSingle();
            f.Position = new Vector3(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
            f.Rotation = Quaternion.Euler(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
            f.Steering = reader.ReadSingle();
            f.ArmsUp = reader.ReadBoolean();
            f.IsBraking = reader.ReadBoolean();
            return f;
        }
    }

    private readonly List<Frame> frames = new List<Frame>();

    /// <inheritdoc />
    public int Version { get; private set; }

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
        using BinaryReader reader = new BinaryReader(ms);

        Version = reader.ReadInt32();
        SteamId = reader.ReadUInt64();
        SoapboxId = reader.ReadInt32();
        HatId = reader.ReadInt32();
        ColorId = reader.ReadInt32();
        int frameCount = reader.ReadInt32();
        for (int i = 0; i < frameCount; i++)
        {
            Frame frame = Frame.Read(reader);
            frames.Add(frame);
        }
    }

    /// <inheritdoc />
    public FrameData GetFrameData(float time)
    {
        Frame startFrame = null;
        Frame endFrame = null;

        foreach (Frame frame in frames)
        {
            if (frame.Time < time)
                startFrame = frame;

            if (frame.Time > time)
            {
                endFrame = frame;
                break;
            }
        }

        if (startFrame != null && endFrame != null)
        {
            float t = Mathf.InverseLerp(startFrame.Time, endFrame.Time, time);

            return new FrameData()
            {
                Position = Vector3.LerpUnclamped(startFrame.Position, endFrame.Position, t),
                Rotation = Quaternion.LerpUnclamped(startFrame.Rotation, endFrame.Rotation, t),
                Steering = Mathf.LerpUnclamped(startFrame.Steering, endFrame.Steering, t),
                ArmsUp = startFrame.ArmsUp || endFrame.ArmsUp,
                IsBraking = startFrame.IsBraking || endFrame.IsBraking
            };
        }

        if (endFrame != null)
        {
            return new FrameData()
            {
                Position = endFrame.Position,
                Rotation = endFrame.Rotation,
                IsBraking = endFrame.IsBraking,
                Steering = endFrame.Steering,
                ArmsUp = endFrame.ArmsUp
            };
        }

        if (startFrame != null)
        {
            return new FrameData()
            {
                Position = startFrame.Position,
                Rotation = startFrame.Rotation,
                IsBraking = startFrame.IsBraking,
                Steering = startFrame.Steering,
                ArmsUp = startFrame.ArmsUp
            };
        }

        return null;
    }
}
