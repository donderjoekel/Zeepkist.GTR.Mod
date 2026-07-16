using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

internal static class GhostSteeringCodec
{
    internal static float FromByte(byte steering)
    {
        return Mathf.Lerp(-1f, 1f, steering / 255f);
    }
}
