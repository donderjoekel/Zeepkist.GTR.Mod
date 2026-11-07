using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TNRD.Zeepkist.GTR.Patching;

public class Patcher : IHostedService, System.IDisposable
{
    private readonly Harmony _harmony = new(MyPluginInfo.PLUGIN_GUID);
    private readonly ILogger<Patcher> _logger;
    private readonly Plugin _plugin;

    public Patcher(ILogger<Patcher> logger, Plugin plugin)
    {
        _logger = logger;
        _plugin = plugin;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Patching");
        if (_plugin.VersionStatus.IsUnsupported)
            _harmony.CreateClassProcessor(typeof(Patches.MainMenuUi_Awake)).Patch();
        else
            _harmony.PatchAll();
        return Task.CompletedTask;
    }

    public void Dispose() => _harmony.UnpatchSelf();

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
