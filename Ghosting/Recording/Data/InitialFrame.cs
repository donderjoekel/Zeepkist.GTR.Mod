using ProtoBuf;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording.Data;

[ProtoContract]
public class InitialFrame
{
    public InitialFrame()
    {
    }

    public InitialFrame(
        Vector3 position,
        Vector3 rotation,
        byte speed,
        byte steering,
        InputFlags inputFlags,
        SoapboxFlags soapboxFlags)
    {
        Position = position;
        Rotation = rotation;
        Speed = speed;
        Steering = steering;
        InputFlags = inputFlags;
        SoapboxFlags = soapboxFlags;
    }

    [ProtoMember(1)] public Vector3 Position { get; set; }
    [ProtoMember(2)] public Vector3 Rotation { get; set; }
    [ProtoMember(3)] public byte Speed { get; set; }
    [ProtoMember(4)] public byte Steering { get; set; }
    [ProtoMember(5)] public InputFlags InputFlags { get; set; }
    [ProtoMember(6)] public SoapboxFlags SoapboxFlags { get; set; }
}
