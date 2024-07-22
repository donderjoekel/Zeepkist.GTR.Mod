using ProtoBuf;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording.Data;

[ProtoContract]
public class DeltaFrame
{
    public DeltaFrame()
    {
    }

    public DeltaFrame(
        float time,
        Vector3Int position,
        Vector3Int rotation,
        byte speed,
        byte steering,
        InputFlags inputFlags,
        SoapboxFlags soapboxFlags)
    {
        Time = time;
        Position = position;
        Rotation = rotation;
        Speed = speed;
        Steering = steering;
        InputFlags = inputFlags;
        SoapboxFlags = soapboxFlags;
    }

    [ProtoMember(1)] public float Time { get; set; }
    [ProtoMember(2)] public Vector3Int Position { get; set; }
    [ProtoMember(3)] public Vector3Int Rotation { get; set; }
    [ProtoMember(4)] public byte Speed { get; set; }
    [ProtoMember(5)] public byte Steering { get; set; }
    [ProtoMember(6)] public InputFlags InputFlags { get; set; }
    [ProtoMember(7)] public SoapboxFlags SoapboxFlags { get; set; }
}
