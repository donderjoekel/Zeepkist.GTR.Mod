using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public class FrameData
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float Steering;
    public bool ArmsUp;
    public bool IsBraking;
}

public class FrameDataWithTime : FrameData
{
    public float Time;
}
