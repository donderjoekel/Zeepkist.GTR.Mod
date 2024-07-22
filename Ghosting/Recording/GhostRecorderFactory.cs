using System;
using Microsoft.Extensions.DependencyInjection;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public class GhostRecorderFactory
{
    private readonly IServiceProvider _provider;

    public GhostRecorderFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    public GhostRecorder Create()
    {
        return _provider.GetService<GhostRecorder>();
    }
}
