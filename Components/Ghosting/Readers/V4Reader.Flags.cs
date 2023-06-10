using System;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public partial class V4Reader
{
    [Flags]
    public enum Flags : byte
    {
        None = 0,
        ArmsUp = 1 << 0,
        IsBraking = 1 << 1,
    }
}
