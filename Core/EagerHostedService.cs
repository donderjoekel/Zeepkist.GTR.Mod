using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Utilities;

namespace TNRD.Zeepkist.GTR.Core;

public class EagerHostedService : IHostedService
{
    private readonly ILogger<EagerHostedService> _logger;
    private readonly IEnumerable<IEagerService> _eagerServices;

    public EagerHostedService(
        ILogger<EagerHostedService> logger,
        [UsedImplicitly] ServiceHelper helper,
        IEnumerable<IEagerService> eagerServices)
    {
        _logger = logger;
        _eagerServices = eagerServices.ToList();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Found {Count} eager services", _eagerServices.Count());

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
