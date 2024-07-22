using ProtoBuf;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording.Data;

[ProtoContract]
public class Cosmetics
{
    [ProtoMember(1)] public int Zeepkist { get; set; }
    [ProtoMember(2)] public int FrontWheels { get; set; }
    [ProtoMember(3)] public int RearWheels { get; set; }
    [ProtoMember(4)] public int Paraglider { get; set; }
    [ProtoMember(5)] public int Horn { get; set; }
    [ProtoMember(6)] public int Hat { get; set; }
    [ProtoMember(7)] public int Glasses { get; set; }
    [ProtoMember(8)] public int ColorBody { get; set; }
    [ProtoMember(9)] public int ColorLeftArm { get; set; }
    [ProtoMember(10)] public int ColorRightArm { get; set; }
    [ProtoMember(11)] public int ColorLeftLeg { get; set; }
    [ProtoMember(12)] public int ColorRightLeg { get; set; }
    [ProtoMember(13)] public int Color { get; set; }
}
