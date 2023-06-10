using System.IO;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public partial class V4Reader
{
    public class ResetFrame : Frame
    {
        public float positionX;
        public float positionY;
        public float positionZ;
        public short rotationX;
        public short rotationY;
        public short rotationZ;
        public short rotationW;
        public byte steering;
        public Flags flags;

        public static ResetFrame Read(BinaryReader reader)
        {
            ResetFrame f = new ResetFrame();
            f.time = reader.ReadSingle();
            f.positionX = reader.ReadSingle();
            f.positionY = reader.ReadSingle();
            f.positionZ = reader.ReadSingle();
            f.rotationX = reader.ReadInt16();
            f.rotationY = reader.ReadInt16();
            f.rotationZ = reader.ReadInt16();
            f.rotationW = reader.ReadInt16();
            f.steering = reader.ReadByte();
            f.flags = (Flags)reader.ReadByte();
            return f;
        }
    }
}
