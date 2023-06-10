using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public class FrameData
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float Steering;
    public bool ArmsUp;
    public bool IsBraking;

    public void CopyFrom(FrameData other)
    {
        Position = other.Position;
        Rotation = other.Rotation;
        Steering = other.Steering;
        ArmsUp = other.ArmsUp;
        IsBraking = other.IsBraking;
    }
}

public class FrameDataWithTime : FrameData
{
    public float Time;

    public void CopyFrom(FrameDataWithTime other)
    {
        base.CopyFrom(other); // Call the base class's CopyFrom method to copy the common fields

        Time = other.Time;
    }
}
