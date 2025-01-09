using System;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

[Flags]
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

public static class SoapboxFlagsExtensions
{
    public static bool HasFlagFast(this SoapboxFlags value, SoapboxFlags flag)
    {
        return (value & flag) != 0;
    }
}
