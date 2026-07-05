using ProtoBuf;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording.Data;

[ProtoContract]
public struct Vector2Int
{
    [ProtoMember(1)] public int X { get; set; }
    [ProtoMember(2)] public int Y { get; set; }

    public Vector2Int()
    {
    }

    public Vector2Int(int x, int y)
    {
        X = x;
        Y = y;
    }

    public static implicit operator UnityEngine.Vector2(Vector2Int v) => new(v.X, v.Y);
}
