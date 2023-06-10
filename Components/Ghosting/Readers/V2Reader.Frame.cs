using System.IO;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public partial class V2Reader
{
    private class Frame
    {
        public float Time { get; private set; }
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }

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
            return f;
        }
    }
}
