using TNRD.Zeepkist.GTR.Ghosting.Playback;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public interface IGhost
{
    void Initialize(GhostVisuals ghost);
    void ApplyCosmetics(string steamName);
    void Start();
    void Stop();
    void Update();
    void FixedUpdate();
}
