using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public interface IFrame
{
    float Time { get; }
    Vector3 Position { get; }
    Quaternion Rotation { get; }
}
