using System.Collections.Generic;
using ProtoBuf;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording.Data;

[ProtoContract]
public class Ghost
{
    [ProtoMember(1)] public int Version { get; set; }
    [ProtoMember(2)] public ulong SteamId { get; set; }
    [ProtoMember(3)] public Cosmetics Cosmetics { get; set; }
    [ProtoMember(4)] public InitialFrame InitialFrame { get; set; }
    [ProtoMember(5)] public List<DeltaFrame> DeltaFrames { get; set; }
    [ProtoMember(6)] public string TaggedUsername { get; set; }
    [ProtoMember(7)] public string Color { get; set; }
}
