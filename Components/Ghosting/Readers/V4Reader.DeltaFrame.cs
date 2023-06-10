using System.IO;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public partial class V4Reader
{
    public class DeltaFrame : Frame
    {
        public short positionX;
        public short positionY;
        public short positionZ;
        public short rotationX;
        public short rotationY;
        public short rotationZ;
        public short rotationW;
        public byte steering;
        public Flags flags;

        public static DeltaFrame Read(BinaryReader reader)
        {
            DeltaFrame f = new DeltaFrame();
            f.time = reader.ReadSingle();
            f.positionX = reader.ReadInt16();
            f.positionY = reader.ReadInt16();
            f.positionZ = reader.ReadInt16();
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
