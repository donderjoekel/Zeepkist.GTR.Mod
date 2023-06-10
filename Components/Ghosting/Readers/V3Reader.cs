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

    private int lastFrameIndex = 0;

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
    public void GetFrameData(float time, ref FrameData frameData)
    {
        frameData ??= new FrameData();

        Frame startFrame = null;
        Frame endFrame = null;

        int i = lastFrameIndex;

        while (i < frames.Count)
        {
            Frame frame = frames[i];

            if (frame.Time > time && startFrame == null && i > 0)
            {
                i--;
                continue;
            }

            if (frame.Time > time)
            {
                endFrame = frame;
                lastFrameIndex = i;
                break;
            }

            startFrame = frame;
            i++;
        }

        if (startFrame != null && endFrame != null)
        {
            float t = Mathf.InverseLerp(startFrame.Time, endFrame.Time, time);

            frameData.Position = Vector3.LerpUnclamped(startFrame.Position, endFrame.Position, t);
            frameData.Rotation = Quaternion.LerpUnclamped(startFrame.Rotation, endFrame.Rotation, t);
            frameData.Steering = Mathf.LerpUnclamped(startFrame.Steering, endFrame.Steering, t);
            frameData.ArmsUp = startFrame.ArmsUp || endFrame.ArmsUp;
            frameData.IsBraking = startFrame.IsBraking || endFrame.IsBraking;
        }
        else if (endFrame != null)
        {
            frameData.Position = endFrame.Position;
            frameData.Rotation = endFrame.Rotation;
            frameData.IsBraking = endFrame.IsBraking;
            frameData.Steering = endFrame.Steering;
            frameData.ArmsUp = endFrame.ArmsUp;
        }
        else if (startFrame != null)
        {
            frameData.Position = startFrame.Position;
            frameData.Rotation = startFrame.Rotation;
            frameData.IsBraking = startFrame.IsBraking;
            frameData.Steering = startFrame.Steering;
            frameData.ArmsUp = startFrame.ArmsUp;
        }
    }
}
