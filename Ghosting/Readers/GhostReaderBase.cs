using System;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

public abstract class GhostReaderBase<TGhost> : IGhostReader where TGhost : IGhost
{
    private readonly IServiceProvider _provider;

    public GhostReaderBase(IServiceProvider provider)
    {
        _provider = provider;
    }

    protected TGhost CreateGhost(params object[] args)
    {
        return ActivatorUtilities.CreateInstance<TGhost>(_provider, args);
    }

    public abstract IGhost Read(byte[] data);
}
