using TNRD.Zeepkist.GTR.Ghosting.Ghosts;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

public interface IGhostReader
{
    IGhost Read(byte[] data);
}
