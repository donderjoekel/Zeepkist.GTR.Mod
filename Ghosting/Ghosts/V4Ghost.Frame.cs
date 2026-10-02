using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public partial class V4Ghost
{
    public readonly struct Frame : IFrame
    {
        public Frame(float time, Vector3 position, Quaternion rotation, float steering, bool armsUp, bool isBraking)
        {
            Time = time;
            Position = position;
            Rotation = rotation;
            Steering = steering;
            ArmsUp = armsUp;
            IsBraking = isBraking;
        }

        public float Time { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float Steering { get; }
        public bool ArmsUp { get; }
        public bool IsBraking { get; }
    }
}
