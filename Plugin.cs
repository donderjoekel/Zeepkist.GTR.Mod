using System;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using BepInEx;
using BepInEx.Bootstrap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Assets;
using TNRD.Zeepkist.GTR.Authentication;
using TNRD.Zeepkist.GTR.Commands;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Connectivity;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Dialogs;
using TNRD.Zeepkist.GTR.Discord;
using TNRD.Zeepkist.GTR.Favourites;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Ghosting.Readers;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using TNRD.Zeepkist.GTR.Leaderboard;
using TNRD.Zeepkist.GTR.Logging;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.Patching;
using TNRD.Zeepkist.GTR.PlayerLoop;
using TNRD.Zeepkist.GTR.UI;
using TNRD.Zeepkist.GTR.UI.Timeline;
using TNRD.Zeepkist.GTR.Users;
using TNRD.Zeepkist.GTR.Utilities;
using TNRD.Zeepkist.GTR.Voting;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Storage;

namespace TNRD.Zeepkist.GTR;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK", "2.5.1")]
public class Plugin : BaseUnityPlugin
{
    private IHost _host;

    private void Awake()
    {
        StartHost().Forget();
    }

    private async UniTaskVoid StartHost()
    {
        try
        {
            await UniTask.WaitUntil(() => Steamworks.SteamClient.IsValid && Steamworks.SteamClient.IsLoggedOn);

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
        services.AddSingleton(new NetworkUserAgent(
            MyPluginInfo.PLUGIN_VERSION,
            Chainloader.PluginInfos["ZeepSDK"].Metadata.Version.ToString()));
        services.AddSingleton<IHostLifetime, NoopHostLifetime>();
        services.AddMemoryCache();
        services.AddEagerService<SpainRoutingService>();
        services.AddEagerService<AuthenticationService>();
        services.AddEagerService<CommandsService>();
        services.AddEagerService<ConfigService>();
        services.AddEagerService<LevelRequestService>();
        services.AddEagerService<OfflineGhostsService>();
        services.AddEagerService<OnlineGhostsService>();
        services.AddEagerService<RecordFeedbackService>();
        services.AddEagerService<RecordingService>();
        services.AddEagerService<PlayerLoopService>();
        services.AddSingleton<BulkGhostModeState>();
        services.AddEagerService<BulkGhostRenderService>();
        services.AddEagerService<GhostPlayer>();
        services.AddEagerService<GhostMaterialService>();
        services.AddEagerService<GhostNamePositioningService>();
        services.AddEagerService<GhostVisibilityService>();
        services.AddEagerService<GhostTimingService>();
        services.AddEagerService<GhostPlaybackService>();
        services.AddEagerService<PhotoModeTimelineService>();
        services.AddEagerService<GtrSpectateTargetService>();
        services.AddEagerService<GtrGhostSpectateRigService>();
        services.AddEagerService<GtrGhostInputDisplayService>();
        services.AddEagerService<PlaybackUiInputState>();
        services.AddSingleton<GhostTimelineState>();
        services.AddSingleton<GhostTimelineDrawer>();
        services.AddSingleton<GtrToolbarDrawer>();
        services.AddEagerService<GhostTimelineUiService>();
        services.AddEagerService<TimelineModeService>();
        services.AddEagerService<GhostTimelineVisibilityService>();
        services.AddEagerService<GhostTimelineOverlayVisibilityService>();
        services.AddEagerService<GhostPlaybackInputService>();
        services.AddEagerService<LeaderboardService>();
        services.AddEagerService<CurrentLevelRecordService>();
        services.AddEagerService<RecordHolderService>();
        services.AddEagerService<DiscordService>();
        services.AddEagerService<LaLigaCensorshipDialogService>();
        services.AddEagerService<UnhandledExceptionLoggerService>();
        services.AddEagerService<VotingService>();
        services.AddSingleton<VotingGraphqlService>();
        services.AddSingleton<AssetService>();
        services.AddSingleton<GhostReaderFactory>();
        services.AddSingleton<GhostRecorderFactory>();
        services.AddSingleton<LeaderboardGraphqlService>();
        services.AddSingleton<TrackTournamentGraphqlService>();
        services.AddSingleton<OnlineLeaderboardTab>();
        services.AddSingleton<OfflineLeaderboardTab>();
        services.AddSingleton<MessengerService>();
        services.AddSingleton<FavouriteService>();
        services.AddSingleton<OnlineGhostGraphqlService>();
        services.AddSingleton<OfflineGhostGraphqlService>();
        services.AddSingleton(_ => StorageApi.CreateModStorage(this));
        services.AddSingleton<LevelBrowser.LevelBrowseService>();
        services.AddSingleton<LevelBrowser.LevelBrowserSession>();
        services.AddSingleton<LevelBrowser.UI.LevelThumbnailCache>();
        services.AddSingleton<LevelBrowser.UI.LevelBrowserWindow>();
        services.AddEagerService<LevelBrowser.UI.LevelBrowserUiService>();
        services.AddEagerService<PlaylistBrowserHost.PlaylistBrowserHostService>();
        services.AddSingleton<ServiceHelper>();
        services.AddSingleton<UserService>();
        services.AddTransient<GhostRecorder>();
        services.AddTransient<V1Reader>();
        services.AddTransient<V2Reader>();
        services.AddTransient<V3Reader>();
        services.AddTransient<V4Reader>();
        services.AddTransient<V5Reader>();
        services.AddTransient<V6Reader>();
        services.AddTransient<V7Reader>();
        services.AddSingleton<ApiHttpClient>();
        services.AddHttpClient();
        services.AddHttpClient(SpainRoutingService.TraceClientKey, (provider, client) =>
        {
            provider.GetRequiredService<NetworkUserAgent>().Apply(client);
            client.BaseAddress = CloudflareTraceRequest.BaseAddress;
            client.Timeout = CloudflareTraceRequest.Timeout;
        });
        services.AddHttpClient(
            AlternativeDomainFallbackHandler.TransportClientKey,
            (provider, client) =>
            {
                provider.GetRequiredService<NetworkUserAgent>().Apply(client);
                client.Timeout = AlternativeDomainFallbackHandler.RequestTimeout;
            });
        services.AddHttpClient(GhostRepository.ClientKey, (provider, client) =>
        {
            provider.GetRequiredService<NetworkUserAgent>().Apply(client);
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddSingleton(provider =>
        {
            var configService = provider.GetRequiredService<ConfigService>();
            return new GhostRepository(
                provider.GetRequiredService<ZeepSDK.Storage.IModStorage>(),
                provider.GetRequiredService<GhostReaderFactory>(),
                provider.GetRequiredService<IHttpClientFactory>().CreateClient(GhostRepository.ClientKey),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GhostRepository>>(),
                configService.MaximumGhostCacheMegabytes.Value * 1024L * 1024L);
        });
        services.AddHttpClient(ApiHttpClient.ClientKey, (provider, client) =>
            {
                var routingService = provider.GetRequiredService<SpainRoutingService>();
                client.BaseAddress = ServiceUriValidator.ParseBaseAddress(
                    routingService.SelectedBackendUrl,
                    "Backend API URL");
                client.Timeout = Timeout.InfiniteTimeSpan;
                provider.GetRequiredService<NetworkUserAgent>().Apply(client);
                AddDefaultHeaders(client);
            })
            .AddHttpMessageHandler(provider => new AlternativeDomainFallbackHandler(
                provider.GetRequiredService<SpainRoutingService>(),
                ServiceEndpoint.Backend,
                () => provider.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(AlternativeDomainFallbackHandler.TransportClientKey)));
        services.AddGtrClient(StrawberryShake.ExecutionStrategy.CacheAndNetwork)
            .ConfigureHttpClient(
                (provider, client) =>
                {
                    var routingService = provider.GetRequiredService<SpainRoutingService>();
                    client.BaseAddress = ServiceUriValidator.ParseBaseAddress(
                        routingService.SelectedGraphQLUrl,
                        "GraphQL URL");
                    client.Timeout = Timeout.InfiniteTimeSpan;
                    provider.GetRequiredService<NetworkUserAgent>().Apply(client);
                    AddDefaultHeaders(client);
                },
                clientBuilder => clientBuilder.AddHttpMessageHandler(provider =>
                    new AlternativeDomainFallbackHandler(
                        provider.GetRequiredService<SpainRoutingService>(),
                        ServiceEndpoint.GraphQL,
                        () => provider.GetRequiredService<IHttpClientFactory>()
                            .CreateClient(AlternativeDomainFallbackHandler.TransportClientKey))))
            .ConfigureWebSocketClient((provider, client) =>
            {
                var routingService = provider.GetRequiredService<SpainRoutingService>();
                Uri graphQlUri = ServiceUriValidator.ParseBaseAddress(
                    routingService.SelectedGraphQLUrl,
                    "GraphQL URL");
                client.Uri = GraphqlWebSocketUri.FromHttp(graphQlUri);
                if (client.Socket is ClientWebSocket socket)
                {
                    provider.GetRequiredService<NetworkUserAgent>()
                        .Apply((name, value) => socket.Options.SetRequestHeader(name, value));
                    AddDefaultHeaders((name, value) => socket.Options.SetRequestHeader(name, value));
                }
            });
    }

    private static void AddDefaultHeaders(HttpClient client)
    {
        AddDefaultHeaders((name, value) => client.DefaultRequestHeaders.Add(name, value));
    }

    private static void AddDefaultHeaders(Action<string, string> addHeader)
    {
        addHeader("X-Zeepkist-Version",
            $"{PlayerManager.Instance.version.version}.{PlayerManager.Instance.version.patch}");
        addHeader("X-Zeepkist-Major-Version", PlayerManager.Instance.version.version.ToString());
        addHeader("X-GTR-Version", MyPluginInfo.PLUGIN_VERSION);
        addHeader("X-Steam-ID", Steamworks.SteamClient.SteamId.ToString());
    }
}
