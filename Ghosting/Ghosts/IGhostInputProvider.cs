namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public interface IGhostInputProvider
{
    bool TrySampleInputAtTime(float time, out GhostInputSample sample);
}
