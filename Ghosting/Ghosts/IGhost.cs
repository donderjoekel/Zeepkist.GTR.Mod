using TNRD.Zeepkist.GTR.Ghosting.Playback;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public interface IGhost
{
    Color Color { get; }
    void Initialize(GhostData ghost);
    void ApplyCosmetics(string steamName);
    void Start();
    void Stop();
    void Update();
    void FixedUpdate();
}
