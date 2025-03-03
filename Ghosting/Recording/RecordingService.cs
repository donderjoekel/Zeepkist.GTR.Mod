using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Messaging;
using ZeepkistClient;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Level;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public class RecordingService : IEagerService
{
    private readonly MessengerService _messengerService;
    private readonly ILogger<RecordingService> _logger;
    private readonly GhostRecorderFactory _ghostRecorderFactory;
    private readonly ApiHttpClient _apiHttpClient;
    private readonly ConfigService _configService;

    private CancellationTokenSource _cancellationTokenSource;
    private GhostRecorder _activeGhostRecorder;

    private bool IsPlayingOnline => ZeepkistNetwork.IsConnectedToGame;
    private bool CanRecord => IsPlayingOnline && _configService.SubmitRecords.Value;

    public RecordingService(
        MessengerService messengerService,
        ILogger<RecordingService> logger,
        GhostRecorderFactory ghostRecorderFactory,
        ApiHttpClient apiHttpClient,
        ConfigService configService)
    {
        _messengerService = messengerService;
        _logger = logger;
        _ghostRecorderFactory = ghostRecorderFactory;
        _apiHttpClient = apiHttpClient;
        _configService = configService;

        RacingApi.PlayerSpawned += OnPlayerSpawned;
        RacingApi.RoundStarted += OnRoundStarted;
        RacingApi.CrossedFinishLine += OnCrossedFinishLine;
        RacingApi.RoundEnded += OnRoundEnded;
    }

    private void OnPlayerSpawned()
    {
        _logger.LogInformation("Stopping existing recorder if any");
        _activeGhostRecorder?.Stop();

        if (!CanRecord)
            return;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        _logger.LogInformation("Creating new recorder");
        _activeGhostRecorder = _ghostRecorderFactory.Create();
    }

    private void OnRoundStarted()
    {
        if (!CanRecord)
            return;

        _logger.LogInformation("Starting recorder");
        _activeGhostRecorder.Start();
    }

    private void OnCrossedFinishLine(float time)
    {
        if (!CanRecord)
            return;

        if (_activeGhostRecorder == null)
            return;

        _logger.LogInformation("Stopping recorder");
        GhostRecorder recorder = _activeGhostRecorder;
        _activeGhostRecorder = null;
        StartSubmit(recorder).Forget();
    }

    private void OnRoundEnded()
    {
        _logger.LogInformation("Discarding recorder");
        _activeGhostRecorder?.Stop();
        _activeGhostRecorder = null;
    }

    private async UniTaskVoid StartSubmit(GhostRecorder ghostRecorder)
    {
        ghostRecorder.Stop();

        _logger.LogInformation("Collecting extra information");
        string hash = LevelApi.CurrentHash;

        if (string.IsNullOrEmpty(hash))
        {
            _messengerService.LogError("Unable to figure out level, discarding record :(");
            _logger.LogError("Unable to get the level hash");
            return;
        }

        WinCompare.Result result = PlayerManager.Instance.currentMaster.playerResults.First();
        List<float> splits = result.split_times.Select(x => x.time).ToList();
        List<float> speeds = result.split_times.Select(x => x.velocity).ToList();
        float time = result.time;

        if (splits.Count != PlayerManager.Instance.currentMaster.racePoints)
        {
            _logger.LogInformation("Discarding any % record");
            return;
        }

        _logger.LogInformation("Processing ghost data");
        string ghostData = ProcessGhostRecorder(ghostRecorder);
        if (string.IsNullOrEmpty(ghostData))
        {
            _logger.LogWarning("Something went wrong here");
        }

        await Submit(hash, time, splits, speeds, ghostData);
    }

    private string ProcessGhostRecorder(GhostRecorder ghostRecorder)
    {
        try
        {
            _logger.LogInformation("Creating stream");
            using MemoryStream stream = new();
            _logger.LogInformation("Writing to stream");
            if (!ghostRecorder.Write(stream))
            {
                _logger.LogError("Failed to write to stream, returning empty string");
                return string.Empty;
            }

            _logger.LogInformation("Closing stream");
            stream.Close();
            _logger.LogInformation("Getting buffer");
            byte[] buffer = stream.ToArray();
            _logger.LogInformation("Converting to base64");
            return Convert.ToBase64String(buffer);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to process ghost data");
            return string.Empty;
        }
    }

    private async UniTask Submit(
        string hash,
        float time,
        List<float> splits,
        List<float> speeds,
        string ghostData)
    {
        _logger.LogInformation("Creating resource");
        RecordPostResource resource = new()
        {
            Level = hash,
            Time = time,
            Splits = splits.ToList(),
            Speeds = speeds.ToList(),
            GhostData = ghostData,
            ModVersion = MyPluginInfo.PLUGIN_VERSION,
            GameVersion = $"{PlayerManager.Instance.version.version}.{PlayerManager.Instance.version.patch}"
        };

        try
        {
            bool loginOrRefresh = await _apiHttpClient.LoginOrRefresh();
            if (!loginOrRefresh)
            {
                _messengerService.LogError("Authentication failed, unable to submit record");
                _logger.LogError("Authentication failed, unable to submit record");
                return;
            }

            HttpResponseMessage response = await _apiHttpClient.PostAsync("records/submit", resource);

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to submit record");
                _messengerService.LogError("Failed to submit record");
                return;
            }

            if (_configService.ShowRecordSubmitMessage.Value)
            {
                _messengerService.LogSuccess("Run submitted");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to submit record");
            _messengerService.LogError("Failed to submit record");
        }
    }
}
