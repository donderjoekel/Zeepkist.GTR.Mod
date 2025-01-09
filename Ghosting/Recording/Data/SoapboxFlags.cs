using ProtoBuf;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording.Data;

[ProtoContract]
public enum SoapboxFlags : byte
{
    None = 0,
    Soap = 1 << 0,
    Offroad = 1 << 1,
    Paraglider = 1 << 2,
    FrontLeft = 1 << 3,
    FrontRight = 1 << 4,
    RearLeft = 1 << 5,
    RearRight = 1 << 6
}
