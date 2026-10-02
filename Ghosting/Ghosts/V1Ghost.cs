using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.GTR.Extensions;
using UnityEngine;
using ZeepSDK.Cosmetics;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public partial class V1Ghost : GhostBase<V1Ghost.Frame>
{
    private readonly Frame[] _frames;

    public V1Ghost(Frame[] frames)
    {
        _frames = frames;
    }

    internal override GhostBase CreatePlayback() => new V1Ghost(_frames);

    protected override int FrameCount => _frames.Length;
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

    protected override float GetFrameTime(int index) => _frames[index].Time;

    protected override Frame GetFrame(int index)
    {
        return _frames[index];
    }
}
