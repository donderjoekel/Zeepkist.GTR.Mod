using BepInEx.Bootstrap;
using BepInEx;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog.Events;
using Serilog;
using StrawberryShake;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System;
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
using TNRD.Zeepkist.GTR.GraphQL;
using TNRD.Zeepkist.GTR.Leaderboard;
using TNRD.Zeepkist.GTR.Logging;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.Patching.Patches;
using TNRD.Zeepkist.GTR.Patching;
using TNRD.Zeepkist.GTR.PlayerLoop;
using TNRD.Zeepkist.GTR.UI.Timeline;
using TNRD.Zeepkist.GTR.UI;
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
    private IHost _bootstrapHost;
    internal GtrVersionStatus VersionStatus { get; private set; } = GtrVersionStatus.Unknown(MyPluginInfo.PLUGIN_VERSION);
    private readonly CancellationTokenSource _startupCancellation = new();
    private (string Name, string Value)[] _defaultHeaders;

    private void Awake()
    {
        StartHost().Forget();
    }

    private async UniTaskVoid StartHost()
    {
        try
        {
            await UniTask.WaitUntil(() => TransportHeaderSnapshot.TryCapture(MyPluginInfo.PLUGIN_VERSION, out _defaultHeaders),
                cancellationToken: _startupCancellation.Token);

            _bootstrapHost = CreateHostBuilder().ConfigureServices(ConfigureBootstrapServices).Build();
            VersionStatus = await GtrStartupGate.CheckAsync(MyPluginInfo.PLUGIN_VERSION,
                () => _bootstrapHost.Services.GetRequiredService<SpainRoutingService>().GetTraceResultAsync(),
                cancellationToken => GtrVersionCheck.ExecuteAsync(
                    _bootstrapHost.Services, MyPluginInfo.PLUGIN_VERSION, cancellationToken),
                exception => Logger.LogWarning($"GTR version check failed; starting normally: {exception.Message}"),
                _startupCancellation.Token);

            await UniTask.SwitchToMainThread();
            _startupCancellation.Token.ThrowIfCancellationRequested();
            _host = CreateHostBuilder().ConfigureServices(ConfigureServices).Build();
            await _host.StartAsync(_startupCancellation.Token);
            await UniTask.SwitchToMainThread();
            bool isMainMenu = UnityEngine.Object.FindObjectOfType<MainMenuUI>() != null;
            bool hasActiveLevel = !VersionStatus.IsUnsupported && !isMainMenu &&
                                  PlayerManager.Instance?.currentMaster != null &&
                                  CurrentLevelGraphqlIdentity.Create().IsAvailable;
            if (isMainMenu)
                MainMenuUi_Awake.NotifyMenuReady();
            else if (hasActiveLevel)
                _host.Services.GetRequiredService<AuthenticationService>().InitializeSession();
            if (hasActiveLevel)
            {
                _host.Services.GetRequiredService<LevelRequestService>().InitializeCurrentLevel();
                _host.Services.GetRequiredService<RecordHolderService>().InitializeCurrentLevel();
                _host.Services.GetRequiredService<CurrentLevelRecordService>().InitializeCurrentLevel();
                _host.Services.GetRequiredService<OnlineGhostsService>().ReloadCurrentLevel();
                _host.Services.GetRequiredService<OfflineGhostsService>().ReloadCurrentLevel();
            }

            // Plugin startup logic
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }
        catch (OperationCanceledException) when (_startupCancellation.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            Logger.LogError("Failed to start plugin");
            Logger.LogError(e);
        }
    }

    private void OnDestroy()
    {
        _startupCancellation.Cancel();
        _host?.Dispose();
        _host = null;
        _bootstrapHost?.Dispose();
        _bootstrapHost = null;
    }

    private IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .UseContentRoot(Path.GetDirectoryName(Info.Location)!)
            .UseSerilog((context, provider, configuration) =>
            {
                configuration.Enrich.FromLogContext();
                configuration.Enrich.FromGlobalLogContext();
                configuration.WriteTo.BepInEx(Logger, restrictedToMinimumLevel: LogEventLevel.Information);
            });
    }

    private void ConfigureBootstrapServices(IServiceCollection services)
    {
        services.AddSingleton(this);
        services.AddSingleton(Config);
        services.AddSingleton<ConfigService>();
        services.AddSingleton(new NetworkUserAgent(MyPluginInfo.PLUGIN_VERSION,
            Chainloader.PluginInfos["ZeepSDK"].Metadata.Version.ToString()));
        services.AddSingleton<SpainRoutingService>();
        services.AddHttpClient(SpainRoutingService.TraceClientKey, (provider, client) =>
        {
            provider.GetRequiredService<NetworkUserAgent>().Apply(client);
            client.BaseAddress = CloudflareTraceRequest.BaseAddress;
            client.Timeout = CloudflareTraceRequest.Timeout;
        });
        services.AddHttpClient(AlternativeDomainFallbackHandler.TransportClientKey,
            (provider, client) => provider.GetRequiredService<NetworkUserAgent>().Apply(client));
        services.AddGtrClient(ExecutionStrategy.NetworkOnly)
            .ConfigureHttpClient((provider, client) =>
            {
                client.BaseAddress = ServiceUriValidator.ParseBaseAddress(
                    provider.GetRequiredService<SpainRoutingService>().SelectedGraphQLUrl, "GraphQL URL");
                client.Timeout = Timeout.InfiniteTimeSpan;
                provider.GetRequiredService<NetworkUserAgent>().Apply(client);
                AddDefaultHeaders(client);
            }, clientBuilder => clientBuilder.AddHttpMessageHandler(provider =>
                new AlternativeDomainFallbackHandler(
                    provider.GetRequiredService<SpainRoutingService>(), ServiceEndpoint.GraphQL,
                    () => provider.GetRequiredService<IHttpClientFactory>()
                        .CreateClient(AlternativeDomainFallbackHandler.TransportClientKey),
                    TimeSpan.FromSeconds(3))));
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Startup already captured headers on Unity thread. Worker factories only read the snapshot.
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
        services.AddSingleton(_bootstrapHost.Services.GetRequiredService<ConfigService>());
        services.AddSingleton(_bootstrapHost.Services.GetRequiredService<SpainRoutingService>());
        services.AddSingleton(VersionStatus);
        if (VersionStatus.IsUnsupported)
        {
            services.AddSingleton<ServiceHelper>();
            services.AddEagerService<PlayerLoopService>();
            services.AddEagerService<UnsupportedVersionService>();
            return;
        }
        services.AddEagerService<AuthenticationService>();
        services.AddEagerService<CommandsService>();
        services.AddEagerService<LevelRequestService>();
        services.AddEagerService<OfflineGhostsService>();
        services.AddEagerService<OnlineGhostsService>();
        services.AddEagerService<RecordFeedbackService>();
        services.AddEagerService<RecordingService>();
        services.AddEagerService<GhostEnablementService>();
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
        services.AddSingleton<RecordHolderGraphqlService>();
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
        services.AddEagerService<GhostLoadDispatcher>();
        services.AddTransient<GhostRecorder>();
        services.AddTransient<V1Reader>();
        services.AddTransient<V2Reader>();
        services.AddTransient<V3Reader>();
        services.AddTransient<V4Reader>();
        services.AddTransient<V5Reader>();
        services.AddTransient<V6Reader>();
        services.AddTransient<V7Reader>();
        services.AddTransient<V8Reader>();
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

    private void AddDefaultHeaders(HttpClient client)
    {
        AddDefaultHeaders((name, value) => client.DefaultRequestHeaders.Add(name, value));
    }

    private void AddDefaultHeaders(Action<string, string> addHeader)
    {
        foreach (var header in _defaultHeaders)
            addHeader(header.Name, header.Value);
    }
}
