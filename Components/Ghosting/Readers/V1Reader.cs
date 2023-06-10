using System.IO;
using TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Curves;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public partial class V1Reader : IGhostReader
{
    private readonly Vector3Curve positionCurve = new();
    private readonly QuaternionCurve rotationCurve = new();

    /// <inheritdoc />
    public int Version => 1;

    /// <inheritdoc />
    public ulong SteamId => 0;

    /// <inheritdoc />
    public int SoapboxId => 0;

    /// <inheritdoc />
    public int HatId => 0;

    /// <inheritdoc />
    public int ColorId => 0;

    /// <inheritdoc />
    public void Read(byte[] buffer)
    {
        using MemoryStream ms = new MemoryStream(buffer);
        using BinaryReader reader = new BinaryReader(ms);

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
