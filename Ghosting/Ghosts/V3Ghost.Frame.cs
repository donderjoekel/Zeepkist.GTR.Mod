using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public partial class V3Ghost
{
    public class Frame : IFrame
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

        public float Time { get; private set; }
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public float Steering { get; private set; }
        public bool ArmsUp { get; private set; }
        public bool IsBraking { get; private set; }
    }
}
