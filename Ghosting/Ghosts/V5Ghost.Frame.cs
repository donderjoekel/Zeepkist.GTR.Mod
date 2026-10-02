using TNRD.Zeepkist.GTR.Ghosting.Recording;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public partial class V5Ghost
{
    public readonly struct Frame : IFrame
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

        public float Time { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float Speed { get; }
        public float Steering { get; }
        public InputFlags InputFlags { get; }
        public SoapboxFlags SoapboxFlags { get; }
    }
}
