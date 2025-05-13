using System;
using System.Diagnostics;
using System.IO;
using BepInEx;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Assets;
using TNRD.Zeepkist.GTR.Authentication;
using TNRD.Zeepkist.GTR.Commands;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Discord;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Ghosting.Readers;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using TNRD.Zeepkist.GTR.Leaderboard;
using TNRD.Zeepkist.GTR.Logging;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.Patching;
using TNRD.Zeepkist.GTR.PlayerLoop;
using TNRD.Zeepkist.GTR.UI;
using TNRD.Zeepkist.GTR.Users;
using TNRD.Zeepkist.GTR.Utilities;
using TNRD.Zeepkist.GTR.Voting;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Storage;

namespace TNRD.Zeepkist.GTR;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK", "1.43.8")]
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
            builder.UseContentRoot(Path.GetDirectoryName(Info.Location)!);
            builder.UseSerilog((context, provider, configuration) =>
            {
                configuration.Enrich.FromLogContext();
                configuration.Enrich.FromGlobalLogContext();
                configuration.WriteTo.BepInEx(Logger, restrictedToMinimumLevel: LogEventLevel.Information);
            });
            builder.ConfigureServices(ConfigureServices);
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

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddHostedService<Patcher>();
        services.AddSingleton<BaseUnityPlugin>(this);
        services.AddSingleton(this);
        services.AddSingleton(Config);
        services.AddSingleton(Logger);
        services.AddSingleton(Info);
        services.AddMemoryCache();
        services.AddEagerService<AuthenticationService>();
        services.AddEagerService<CommandsService>();
        services.AddEagerService<ConfigService>();
        services.AddEagerService<OfflineGhostsService>();
        services.AddEagerService<OnlineGhostsService>();
        services.AddEagerService<RecordingService>();
        services.AddEagerService<PlayerLoopService>();
        services.AddEagerService<GhostPlayer>();
        services.AddEagerService<GhostMaterialService>();
        services.AddEagerService<GhostNamePositioniongService>();
        services.AddEagerService<GhostVisibilityService>();
        services.AddEagerService<GhostTimingService>();
        services.AddEagerService<LeaderboardService>();
        services.AddEagerService<RecordHolderService>();
        services.AddEagerService<DiscordService>();
        services.AddEagerService<UnhandledExceptionLoggerService>();
        services.AddEagerService<VotingService>();
        services.AddSingleton<AssetService>();
        services.AddSingleton<GhostRepository>();
        services.AddSingleton<GhostReaderFactory>();
        services.AddSingleton<GhostRecorderFactory>();
        services.AddSingleton<LeaderboardGraphqlService>();
        services.AddSingleton<OnlineLeaderboardTab>();
        services.AddSingleton<OfflineLeaderboardTab>();
        services.AddSingleton<MessengerService>();
        services.AddSingleton<OnlineGhostGraphqlService>();
        services.AddSingleton<OfflineGhostGraphqlService>();
        services.AddSingleton(_ => StorageApi.CreateModStorage(this));
        services.AddSingleton<RecordHolderGraphqlService>();
        services.AddSingleton<WorldRecordCommandGraphQlService>();
        services.AddSingleton<ServiceHelper>();
        services.AddSingleton<UserService>();
        services.AddTransient<GhostRecorder>();
        services.AddTransient<V1Reader>();
        services.AddTransient<V2Reader>();
        services.AddTransient<V3Reader>();
        services.AddTransient<V4Reader>();
        services.AddTransient<V5Reader>();
        services.AddHttpClient<ApiHttpClient>();
        services.AddHttpClient<GraphQLApiHttpClient>();
        services.AddHttpClient();
        services.AddGtrClient()
            .ConfigureHttpClient(x => x.BaseAddress = new Uri("https://graphql.zeepki.st"));
    }

    private async UniTaskVoid StopHost()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}