using System;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

[Flags]
public enum InputFlags : byte
{
    None = 0,
    ArmsUp = 1 << 0,
    Braking = 1 << 1,
    Horn = 1 << 2,
}

public static class InputFlagsExtensions
{
    public static bool HasFlagFast(this InputFlags value, InputFlags flag)
    {
        return (value & flag) != 0;
    }
}
