using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public partial class V2Ghost
{
    public readonly struct Frame : IFrame
    {
        public Frame(float time, Vector3 position, Quaternion rotation)
        {
            Time = time;
            Position = position;
            Rotation = rotation;
        }

        public float Time { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }
}
