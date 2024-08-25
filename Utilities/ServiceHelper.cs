using System;

namespace TNRD.Zeepkist.GTR.Utilities;

public class ServiceHelper : IServiceProvider
{
    private readonly IServiceProvider _provider;

    public static IServiceProvider Instance { get; private set; }

    public ServiceHelper(IServiceProvider provider)
    {
        _provider = provider;
        Instance = this;
    }

    public object GetService(Type serviceType)
    {
        return _provider.GetService(serviceType);
    }
}
