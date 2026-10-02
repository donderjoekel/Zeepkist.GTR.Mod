using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Extensions;
using UnityEngine;
using ZeepSDK.Cosmetics;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public partial class V4Ghost : GhostBase<V4Ghost.Frame>, IGhostInputProvider
{
    private readonly ulong _steamId;
    private readonly int _soapboxId;
    private readonly int _hatId;
    private readonly int _colorId;
    private readonly Frame[] _frames;

    public V4Ghost(
        ulong steamId,
        int soapboxId,
        int hatId,
        int colorId,
        Frame[] frames)
    {
        _steamId = steamId;
        _soapboxId = soapboxId;
        _hatId = hatId;
        _colorId = colorId;
        _frames = frames;
    }

    internal override GhostBase CreatePlayback() => new V4Ghost(_steamId, _soapboxId, _hatId, _colorId, _frames);

    protected override int FrameCount => _frames.Length;
    public override Color Color => CosmeticsApi.GetColor(_colorId, false).skinColor.color;

    public override void ApplyCosmetics(string steamName)
    {
        CosmeticsV16 cosmetics = new();
        cosmetics.FromPreV16(_soapboxId, _hatId, _colorId);
        SetupCosmetics(cosmetics, steamName, _steamId);
    }

    protected override float GetFrameTime(int index) => _frames[index].Time;

    protected override Frame GetFrame(int index)
    {
        return _frames[index];
    }

    public bool TrySampleInputAtTime(float time, out GhostInputSample sample)
    {
        return GhostInputFrameSampler.TrySample(
            _frames,
            time,
            frame => frame.Time,
            frame => frame.Steering,
            frame => frame.ArmsUp,
            frame => frame.IsBraking,
            out sample);
    }
}
