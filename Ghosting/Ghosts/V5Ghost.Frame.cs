using TNRD.Zeepkist.GTR.Ghosting.Recording;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public partial class V5Ghost
{
    public class Frame : IFrame
    {
        public Frame(
            float time,
            Vector3 position,
            Quaternion rotation,
            float speed,
            float steering,
            InputFlags inputFlags,
            SoapboxFlags soapboxFlags)
        {
            Time = time;
            Position = position;
            Rotation = rotation;
            Speed = speed;
            Steering = steering;
            InputFlags = inputFlags;
            SoapboxFlags = soapboxFlags;
        }

        public float Time { get; private set; }
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public float Speed { get; private set; }
        public float Steering { get; private set; }
        public InputFlags InputFlags { get; private set; }
        public SoapboxFlags SoapboxFlags { get; private set; }
    }
}
