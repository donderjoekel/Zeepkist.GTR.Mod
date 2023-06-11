using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Curves;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public partial class V4Reader : IGhostReader
{
    private byte precision;

    private readonly Vector3Curve positionCurve = new();
    private readonly QuaternionCurve rotationCurve = new();

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
                    lastResetFrame = ReadFrame(reader, deltaFrames);
                }
                else
                {
                    ReadDeltaFrame(reader, deltaFrames, lastResetFrame);
                }
            }
        }
        finally
        {
            zip?.Dispose();
            reader.Dispose();
        }
    }

    private ResetFrame ReadFrame(BinaryReader reader, List<DeltaFrame> deltaFrames)
    {
        ResetFrame lastResetFrame = ResetFrame.Read(reader);
        deltaFrames.Clear();

        positionCurve.Add(lastResetFrame.time,
            lastResetFrame.positionX,
            lastResetFrame.positionY,
            lastResetFrame.positionZ);

        rotationCurve.Add(lastResetFrame.time,
            ShortToFloat(lastResetFrame.rotationX, 30000),
            ShortToFloat(lastResetFrame.rotationY, 30000),
            ShortToFloat(lastResetFrame.rotationZ, 30000),
            ShortToFloat(lastResetFrame.rotationW, 30000));
        return lastResetFrame;
    }

    private void ReadDeltaFrame(BinaryReader reader, List<DeltaFrame> deltaFrames, ResetFrame lastResetFrame)
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

        positionCurve.Add(deltaFrame.time, position);
        rotationCurve.Add(deltaFrame.time, rotation);
    }

    private static bool IsGZipped(byte[] buffer)
    {
        return buffer[0] == 0x1f && buffer[1] == 0x8b;
    }

    /// <inheritdoc />
    public void GetFrameData(float time, ref FrameData frameData)
    {
        frameData ??= new FrameData();

        frameData.Position = positionCurve.Evaluate(time);
        frameData.Rotation = rotationCurve.Evaluate(time);
    }

    private static float ShortToFloat(short value, float scale = 10000f)
    {
        return value / scale;
    }
}
