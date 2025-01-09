using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.GTR.Extensions;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using UnityEngine;
using ZeepSDK.Cosmetics;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public partial class V1Ghost : GhostBase
{
    private readonly List<Frame> _frames;

    public V1Ghost(GhostTimingService timingService, List<Frame> frames) : base(timingService)
    {
        _frames = frames;
    }

    protected override int FrameCount => _frames.Count;
    public override Color Color => Color.white;

    public override void ApplyCosmetics(string steamName)
    {
        CosmeticsV16 cosmetics = new();
        cosmetics.FromPreV16(
            CosmeticsApi.GetAllZeepkists().First().itemID,
            CosmeticsApi.GetAllHats().First().itemID,
            CosmeticsApi.GetAllColors().First().itemID);
        SetupCosmetics(cosmetics, steamName, 0);
    }

    protected override IFrame GetFrame(int index)
    {
        return _frames[index];
    }
}
