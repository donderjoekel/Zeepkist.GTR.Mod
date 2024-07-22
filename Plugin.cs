using System;
using BepInEx;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Assets;
using TNRD.Zeepkist.GTR.Authentication;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Ghosting;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Ghosting.Readers;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using TNRD.Zeepkist.GTR.Logging;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.Patching;
using TNRD.Zeepkist.GTR.PlayerLoop;
using TNRD.Zeepkist.GTR.Screenshots;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Storage;

namespace TNRD.Zeepkist.GTR
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("ZeepSDK", "1.11.1")]
    public class Plugin : BaseUnityPlugin
    {
        private IHost _host;

        private void Awake()
        {
            StartHost().Forget();
        }

        private void OnDestroy()
        {
            StopHost().Forget();
        }

        private async UniTaskVoid StartHost()
        {
            try
            {
                IHostBuilder builder = Host.CreateDefaultBuilder();
                builder.UseSerilog((context, provider, configuration) => { configuration.WriteTo.BepInEx(Logger); });
                builder.ConfigureServices(
                    services =>
                    {
                        services.AddHostedService<Patcher>();
                        services.AddSingleton<BaseUnityPlugin>(this);
                        services.AddSingleton(this);
                        services.AddSingleton(Config);
                        services.AddSingleton(Logger);
                        services.AddSingleton(Info);
                        services.AddEagerService<AuthenticationService>();
                        services.AddEagerService<ConfigService>();
                        services.AddEagerService<OfflineGhostsService>();
                        services.AddEagerService<OnlineGhostsService>();
                        services.AddEagerService<RecordingService>();
                        services.AddEagerService<PlayerLoopService>();
                        services.AddEagerService<GhostPlayer>();
                        services.AddEagerService<GhostMaterialService>();
                        services.AddSingleton<AssetService>();
                        services.AddSingleton<GhostRepository>();
                        services.AddSingleton<GhostReaderFactory>();
                        services.AddSingleton<GhostRecorderFactory>();
                        services.AddSingleton<MessengerService>();
                        services.AddSingleton<ScreenshotService>();
                        services.AddSingleton(_ => StorageApi.CreateModStorage(this));
                        services.AddTransient<GhostRecorder>();
                        services.AddHttpClient<ApiHttpClient>();
                        services.AddHttpClient<JsonApiHttpClient>();
                        services.AddHttpClient<GraphQLApiHttpClient>();
                        services.AddHttpClient();
                    });
                _host = builder.Build();
                await _host.StartAsync();

                // Plugin startup logic
                Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            }
            catch (Exception e)
            {
                Logger.LogError("Failed to start plugin");
                Logger.LogError(e);
            }
        }

        private async UniTaskVoid StopHost()
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
