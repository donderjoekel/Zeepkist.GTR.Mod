using Microsoft.Extensions.DependencyInjection;

namespace TNRD.Zeepkist.GTR.Core;

public static class EagerExtensions
{
    public static IServiceCollection AddEagerService<TConcrete>(this IServiceCollection services)
        where TConcrete : class, IEagerService
    {
        services.AddHostedService<EagerHostedService>();
        services.AddSingleton<TConcrete>();
        services.AddSingleton<IEagerService>(provider => provider.GetRequiredService<TConcrete>());
        return services;
    }

    public static IServiceCollection AddEagerService<TAbstract, TConcrete>(this IServiceCollection services)
        where TAbstract : class, IEagerService
        where TConcrete : class, TAbstract, IEagerService
    {
        services.AddHostedService<EagerHostedService>();
        services.AddSingleton<TConcrete>();
        services.AddSingleton<TAbstract>(provider => provider.GetRequiredService<TConcrete>());
        services.AddSingleton<IEagerService>(provider => provider.GetRequiredService<TConcrete>());
        return services;
    }
}
