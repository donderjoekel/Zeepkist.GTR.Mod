using System;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

[Flags]
public enum SurfaceState : byte
{
    None = 0,
    Tarmac = 1 << 0,
    Grass = 1 << 1,
    Sand = 1 << 2,
    Snow = 1 << 3,
    Ice = 1 << 4,
    Soap = 1 << 5,
    Metal = 1 << 6
}
