using ProtoBuf;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording.Data;

[ProtoContract]
public enum InputFlags : byte
{
    None = 0,
    ArmsUp = 1 << 0,
    Braking = 1 << 1,
    Horn = 1 << 2,
}
