﻿using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
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
using TNRD.Zeepkist.GTR.Leaderboard;
using TNRD.Zeepkist.GTR.Logging;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.Patching;
using TNRD.Zeepkist.GTR.PlayerLoop;
using TNRD.Zeepkist.GTR.Screenshots;
using UnityEngine;
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
                        services.AddEagerService<GhostNamePositioniongService>();
                        services.AddEagerService<GhostVisibilityService>();
                        services.AddEagerService<LeaderboardService>();
                        services.AddSingleton<AssetService>();
                        services.AddSingleton<GhostRepository>();
                        services.AddSingleton<GhostReaderFactory>();
                        services.AddSingleton<GhostRecorderFactory>();
                        services.AddSingleton<LeaderboardGraphqlService>();
                        services.AddSingleton<OnlineLeaderboardTab>();
                        services.AddSingleton<OfflineLeaderboardTab>();
                        services.AddSingleton<MessengerService>();
                        services.AddSingleton<ScreenshotService>();
                        services.AddSingleton<OnlineGhostGraphqlService>();
                        services.AddSingleton<OfflineGhostGraphqlService>();
                        services.AddSingleton(_ => StorageApi.CreateModStorage(this));
                        services.AddTransient<GhostRecorder>();
                        services.AddTransient<V1Reader>();
                        services.AddTransient<V2Reader>();
                        services.AddTransient<V3Reader>();
                        services.AddTransient<V4Reader>();
                        services.AddTransient<V5Reader>();
                        services.AddHttpClient<ApiHttpClient>();
                        services.AddHttpClient<JsonApiHttpClient>();
                        services.AddHttpClient<GraphQLApiHttpClient>();
                        services.AddHttpClient();
                    });
                _host = builder.Build();
                await _host.StartAsync();

                // Plugin startup logic
                Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

                ServicePointManager.ServerCertificateValidationCallback += ValidateCertificate;
            }
            catch (Exception e)
            {
                Logger.LogError("Failed to start plugin");
                Logger.LogError(e);
            }
        }

        private bool ValidateCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            if (sender is not HttpWebRequest request)
                return false;

            return request.RequestUri.ToString().StartsWith(
                       "https://cdn.zeepkist-gtr.com",
                       StringComparison.OrdinalIgnoreCase)
                   &&
                   certificate.Subject.StartsWith(
                       "CN=*.eu-central-1.wasabisys.com",
                       StringComparison.OrdinalIgnoreCase);
        }

        private async UniTaskVoid StopHost()
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
