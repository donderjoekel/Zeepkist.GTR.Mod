using System.IO;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public partial class V3Reader
{
    private class Frame
    {
        public float Time { get; private set; }
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public float Steering { get; private set; }
        public bool ArmsUp { get; private set; }
        public bool IsBraking { get; private set; }

        public static Frame Read(BinaryReader reader)
        {
            Frame f = new Frame();
            f.Time = reader.ReadSingle();
            f.Position = new Vector3(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
            f.Rotation = Quaternion.Euler(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
            f.Steering = reader.ReadSingle();
            f.ArmsUp = reader.ReadBoolean();
            f.IsBraking = reader.ReadBoolean();
            return f;
        }
    }
}
