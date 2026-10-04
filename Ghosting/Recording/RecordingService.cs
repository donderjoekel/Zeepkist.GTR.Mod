using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.GraphQL;
using TNRD.Zeepkist.GTR.Ghosting.Readers;
using TNRD.Zeepkist.GTR.Utilities;
using ZeepkistClient;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Level;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public class RecordingService : IEagerService, IDisposable
{
    private readonly MessengerService _messengerService;
    private readonly ILogger<RecordingService> _logger;
    private readonly GhostRecorderFactory _ghostRecorderFactory;
    private readonly ApiHttpClient _apiHttpClient;
    private readonly ConfigService _configService;
    private readonly RecordFeedbackService _recordFeedbackService;

    private GhostRecorder _activeGhostRecorder;
    private readonly SerialPipeline<PendingRecording, PreparedRecording> _publishing;

    private sealed class PendingRecording : IDisposable
    {
        public GhostRecorder.Snapshot Snapshot;
        public RecordPostResource Resource;
        public RecordFeedbackBaseline Baseline;
        public void Dispose() { Snapshot.Dispose(); Resource.GhostData = null; }
    }

    private sealed class PreparedRecording
    {
        public string Json;
        public float Time;
        public RecordFeedbackBaseline Baseline;
    }

    private bool IsPlayingOnline => ZeepkistNetwork.IsConnectedToGame;
    private bool CanRecord => IsPlayingOnline && _configService.SubmitRecords.Value;

    public RecordingService(
        MessengerService messengerService,
        ILogger<RecordingService> logger,
        GhostRecorderFactory ghostRecorderFactory,
        ApiHttpClient apiHttpClient,
        ConfigService configService,
        RecordFeedbackService recordFeedbackService)
    {
        _messengerService = messengerService;
        _logger = logger;
        _ghostRecorderFactory = ghostRecorderFactory;
        _apiHttpClient = apiHttpClient;
        _configService = configService;
        _recordFeedbackService = recordFeedbackService;
        _publishing = new SerialPipeline<PendingRecording, PreparedRecording>(
            EncodeRecording, UploadRecording, error => ReportPublishingFailure(error).Forget());

        RacingApi.PlayerSpawned += OnPlayerSpawned;
        RacingApi.RoundStarted += OnRoundStarted;
        RacingApi.CrossedFinishLine += OnCrossedFinishLine;
        RacingApi.RoundEnded += OnRoundEnded;
        RacingApi.Quit += OnRoundEnded;
        MultiplayerApi.DisconnectedFromGame += OnRoundEnded;
    }

    private void OnPlayerSpawned()
    {
        _logger.LogInformation("Stopping existing recorder if any");
        _activeGhostRecorder?.Stop();
        _activeGhostRecorder = null;

        if (!CanRecord)
            return;

        _logger.LogInformation("Creating new recorder");
        _activeGhostRecorder = _ghostRecorderFactory.Create();
    }

    private void OnRoundStarted()
    {
        if (!CanRecord)
            return;

        _logger.LogInformation("Starting recorder");
        _activeGhostRecorder?.Start();
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
        try { StartSubmit(recorder); }
        catch (Exception error) { ReportPublishingFailure(error).Forget(); }
        finally { recorder.Stop(); }
    }

    private void OnRoundEnded()
    {
        _logger.LogInformation("Discarding recorder");
        _activeGhostRecorder?.Stop();
        _activeGhostRecorder = null;
    }

    private void StartSubmit(GhostRecorder ghostRecorder)
    {
        _logger.LogInformation("Collecting extra information");
        LevelHashV2 currentHash = LevelApi.CurrentHashV2;
        string hash = currentHash?.ZeepHash;
        string canonicalHash = currentHash?.Hash;
        LevelScriptableObject currentLevel = LevelApi.CurrentLevel;
        string workshopId = RecordWorkshopId.ToWireValue(
            ZeepkistNetwork.CurrentLobby?.WorkshopID ?? 0,
            currentLevel?.WorkshopID ?? 0,
            currentLevel?.IsAdventureLevel ?? false,
            currentLevel?.UseAvonturenLevel ?? false);
        WinCompare.Result result = PlayerManager.Instance.currentMaster.playerResults.First();
        List<float> splits = result.split_times.Select(x => x.time).ToList();
        List<float> speeds = result.split_times.Select(x => x.velocity).ToList();
        float time = result.time;
        ghostRecorder.CaptureFinishFrame(time);
        ghostRecorder.Stop();

        _logger.LogInformation(
            "Resolved workshop ID {WorkshopId} from lobby {LobbyWorkshopId} and level {LevelWorkshopId}",
            workshopId ?? "<none>",
            ZeepkistNetwork.CurrentLobby?.WorkshopID ?? 0,
            currentLevel?.WorkshopID ?? 0);

        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(canonicalHash))
        {
            _messengerService.LogError("Unable to figure out level, discarding record :(");
            _logger.LogError("Unable to get the level hash");
            return;
        }

        if (splits.Count != PlayerManager.Instance.currentMaster.racePoints)
        {
            _logger.LogInformation("Discarding any % record");
            return;
        }

        if (!RecordSubmissionEligibility.ShouldSubmit(currentLevel?.UID))
        {
            _logger.LogInformation("Discarding record for non-replayable TRTM level");
            return;
        }

        var pending = new PendingRecording
        {
            Snapshot = ghostRecorder.Freeze(),
            Resource = new RecordPostResource
            {
                Level = hash,
                Hash = canonicalHash,
                WorkshopId = workshopId,
                Time = time,
                Splits = splits,
                Speeds = speeds,
                ModVersion = MyPluginInfo.PLUGIN_VERSION,
                GameVersion = $"{PlayerManager.Instance.version.version}.{PlayerManager.Instance.version.patch}"
            },
            Baseline = _recordFeedbackService.CaptureBaseline()
        };
        try { _publishing.Enqueue(pending); }
        catch { pending.Dispose(); throw; }
    }

    private static Task<PreparedRecording> EncodeRecording(PendingRecording pending, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using LimitedMemoryStream stream = new(GhostLimits.MaxCompressedBytes);
        pending.Snapshot.Write(stream);
        token.ThrowIfCancellationRequested();
        pending.Resource.GhostData = Convert.ToBase64String(stream.GetBuffer(), 0, checked((int)stream.Length));
        return Task.FromResult(new PreparedRecording
        {
            Json = JsonConvert.SerializeObject(pending.Resource),
            Time = pending.Resource.Time,
            Baseline = pending.Baseline
        });
    }

    private async Task UploadRecording(PreparedRecording recording, CancellationToken token)
    {
        using HttpResponseMessage response = await _apiHttpClient.PostJsonAsync("record/submit", recording.Json, token);
        await ApiResponseErrors.EnsureSuccessWithBodyAsync(response);
        await UniTask.SwitchToMainThread(cancellationToken: token);
        if (_configService.ShowRecordSubmitMessage.Value)
            _messengerService.LogSuccess("Run submitted", _configService.ShowRecordSubmitMessageDuration.Value);

        if (recording.Baseline?.LevelKey == CurrentLevelGraphqlIdentity.Create().CacheKey)
            _recordFeedbackService.HandleSuccessfulSubmission(recording.Time, recording.Baseline);
    }

    private async UniTaskVoid ReportPublishingFailure(Exception error)
    {
        _logger.LogError(error, "Failed to submit record");
        await UniTask.SwitchToMainThread();
        _messengerService.LogError("Failed to submit record");
    }

    public void Dispose()
    {
        RacingApi.PlayerSpawned -= OnPlayerSpawned;
        RacingApi.RoundStarted -= OnRoundStarted;
        RacingApi.CrossedFinishLine -= OnCrossedFinishLine;
        RacingApi.RoundEnded -= OnRoundEnded;
        RacingApi.Quit -= OnRoundEnded;
        MultiplayerApi.DisconnectedFromGame -= OnRoundEnded;
        _activeGhostRecorder?.Stop();
        _activeGhostRecorder = null;
        _publishing.Dispose();
    }
}
