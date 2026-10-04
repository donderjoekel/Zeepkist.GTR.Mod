// Capture the real readers' constructor arguments without creating Unity visuals.
namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts
{
    public interface IGhost;
    public interface ICapturedFrames : IGhost { Array Frames { get; set; } }
    public partial class V1Ghost : ICapturedFrames { public Array Frames { get; set; } }
    public partial class V2Ghost : ICapturedFrames { public Array Frames { get; set; } }
    public partial class V3Ghost : ICapturedFrames { public Array Frames { get; set; } }
    public partial class V4Ghost : ICapturedFrames { public Array Frames { get; set; } }
    public partial class V5Ghost : ICapturedFrames { public Array Frames { get; set; } }
    public partial class V6Ghost : ICapturedFrames { public Array Frames { get; set; } }
}

namespace TNRD.Zeepkist.GTR.Ghosting.Readers
{
    public abstract class GhostReaderBase<TGhost> where TGhost : Ghosts.ICapturedFrames, new()
    {
        protected GhostReaderBase(IServiceProvider provider) { }
        protected TGhost CreateGhost(params object[] arguments) => new() { Frames = (Array)arguments[^1] };
        public abstract Ghosts.IGhost Read(byte[] data);
    }
}

namespace TNRD.Zeepkist.GTR.Utilities
{
    public static class ColorUtilities
    {
        public static UnityEngine.Color FromHexString(string value) => default;
    }
}

namespace ZeepkistNetworking
{
    public class CosmeticIDs
    {
        public int color, color_body, color_leftArm, color_leftLeg, color_rightArm, color_rightLeg;
        public int frontWheels, glasses, hat, horn, paraglider, rearWheels, zeepkist;
    }
}
