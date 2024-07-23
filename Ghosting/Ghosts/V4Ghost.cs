using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Extensions;
using UnityEngine;
using ZeepSDK.Cosmetics;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public partial class V4Ghost : GhostBase
{
    private readonly ulong _steamId;
    private readonly int _soapboxId;
    private readonly int _hatId;
    private readonly int _colorId;
    private readonly List<Frame> _frames;

    public V4Ghost(ulong steamId, int soapboxId, int hatId, int colorId, List<Frame> frames)
    {
        _steamId = steamId;
        _soapboxId = soapboxId;
        _hatId = hatId;
        _colorId = colorId;
        _frames = frames;
    }

    protected override int FrameCount => _frames.Count;
    public override Color Color => CosmeticsApi.GetColor(_colorId, false).skinColor.color;

    public override void ApplyCosmetics(string steamName)
    {
        CosmeticsV16 cosmetics = new();
        cosmetics.FromPreV16(_soapboxId, _hatId, _colorId);
        SetupCosmetics(cosmetics, steamName, _steamId);
    }

    protected override IFrame GetFrame(int index)
    {
        return _frames[index];
    }
}
