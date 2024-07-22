using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TNRD.Zeepkist.GTR.Patching;

public class Patcher : IHostedService
{
    private readonly Harmony _harmony = new(MyPluginInfo.PLUGIN_GUID);
    private readonly ILogger<Patcher> _logger;

    public Patcher(ILogger<Patcher> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Patching");
        _harmony.PatchAll();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Unpatching");
        _harmony.UnpatchSelf();
        return Task.CompletedTask;
    }
}
