using System;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

[Flags]
public enum WheelState : byte
{
    HasNone = 0,
    HasFrontLeft = 1 << 0,
    HasFrontRight = 1 << 1,
    HasRearLeft = 1 << 2,
    HasRearRight = 1 << 3,
    HasFront = HasFrontLeft | HasFrontRight,
    HasRear = HasRearLeft | HasRearRight,
    HasAll = HasFront | HasRear
}
