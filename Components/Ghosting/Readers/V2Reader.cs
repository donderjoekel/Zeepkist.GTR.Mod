using System.IO;
using TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Curves;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public partial class V2Reader : IGhostReader
{
    private readonly Vector3Curve positionCurve = new();
    private readonly QuaternionCurve rotationCurve = new();

    /// <inheritdoc />
    public int Version => 2;

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
        using BinaryReader reader = new BinaryReader(ms);

        int version = reader.ReadInt32();
        SteamId = reader.ReadUInt64();
        SoapboxId = reader.ReadInt32();
        HatId = reader.ReadInt32();
        ColorId = reader.ReadInt32();
        int frameCount = reader.ReadInt32();
        for (int i = 0; i < frameCount; i++)
        {
            Frame frame = Frame.Read(reader);
            positionCurve.Add(frame.Time, frame.Position);
            rotationCurve.Add(frame.Time, frame.Rotation);
        }
    }

    /// <inheritdoc />
    public void GetFrameData(float time, ref FrameData frameData)
    {
        frameData ??= new FrameData();

        frameData.Position = positionCurve.Evaluate(time);
        frameData.Rotation = rotationCurve.Evaluate(time);
        frameData.ArmsUp = false;
        frameData.IsBraking = false;
        frameData.Steering = 0;
    }
}
