using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TNRD.Zeepkist.GTR.Core;

public class EagerHostedService : IHostedService
{
    private readonly ILogger<EagerHostedService> _logger;
    private readonly IEnumerable<IEagerService> _eagerServices;

    public EagerHostedService(ILogger<EagerHostedService> logger, IEnumerable<IEagerService> eagerServices)
    {
        _logger = logger;
        _eagerServices = eagerServices;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Found {Count} eager services", _eagerServices.Count());
        foreach (IEagerService eagerService in _eagerServices)
        {
            _logger.LogInformation("Found eager service '{Service}'", eagerService.GetType().Name);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
