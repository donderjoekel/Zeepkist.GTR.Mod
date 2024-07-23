using System.Collections.Generic;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public partial class V1Ghost : GhostBase
{
    private readonly List<Frame> _frames;

    public V1Ghost(List<Frame> frames)
    {
        _frames = frames;
    }

    protected override int FrameCount => _frames.Count;
    public override Color Color => Color.white;

    public override void ApplyCosmetics(string steamName)
    {
        // TODO: Apply cosmetics
    }

    protected override IFrame GetFrame(int index)
    {
        return _frames[index];
    }
}
