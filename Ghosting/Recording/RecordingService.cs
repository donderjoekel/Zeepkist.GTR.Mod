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
        public string CaptureDirectory;
        public string LevelSource;
        public void Dispose() { Snapshot.Dispose(); Resource.GhostData = null; }
    }

    private sealed class PreparedRecording
    {
        public string Json;
        public float Time;
        public RecordFeedbackBaseline Baseline;
        public bool Captured;
    }

    private bool IsPlayingOnline => ZeepkistNetwork.IsConnectedToGame;
    private bool CanRecord => _configService.CaptureValidationFixtures.Value || (IsPlayingOnline && _configService.SubmitRecords.Value);

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
        if (_configService.CaptureValidationFixtures.Value) _activeGhostRecorder.EnableValidationMetrics();
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
        PendingRecording pending;
        try { pending = PrepareSubmission(); }
        catch (Exception error) { recorder.Stop(); ReportPublishingFailure(error).Forget(); return; }
        SubmitAfterTrigger(recorder, pending).Forget();
    }

    private async UniTaskVoid SubmitAfterTrigger(GhostRecorder recorder, PendingRecording pending)
    {
        // Finish event fires inside HeyYouHitATrigger. Its postfix records accepted geometry first.
        await UniTask.Yield(ZeepSDK.External.Cysharp.Threading.Tasks.PlayerLoopTiming.LastFixedUpdate);
        try {
            if (pending == null) return;
            recorder.CaptureFinishFrame(pending.Resource.Time);
            pending.Snapshot = recorder.Freeze();
            try { _publishing.Enqueue(pending); }
            catch { pending.Dispose(); throw; }
        }
        catch (Exception error) { ReportPublishingFailure(error).Forget(); }
        finally { recorder.Stop(); }
    }

    private void OnRoundEnded()
    {
        _logger.LogInformation("Discarding recorder");
        _activeGhostRecorder?.Stop();
        _activeGhostRecorder = null;
    }

    private PendingRecording PrepareSubmission()
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

        _logger.LogInformation(
            "Resolved workshop ID {WorkshopId} from lobby {LobbyWorkshopId} and level {LevelWorkshopId}",
            workshopId ?? "<none>",
            ZeepkistNetwork.CurrentLobby?.WorkshopID ?? 0,
            currentLevel?.WorkshopID ?? 0);

        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(canonicalHash))
        {
            _messengerService.LogError("Unable to figure out level, discarding record :(");
            _logger.LogError("Unable to get the level hash");
            return null;
        }

        if (splits.Count != PlayerManager.Instance.currentMaster.racePoints)
        {
            _logger.LogInformation("Discarding any % record");
            return null;
        }

        if (!RecordSubmissionEligibility.ShouldSubmit(currentLevel?.UID))
        {
            _logger.LogInformation("Discarding record for non-replayable TRTM level");
            return null;
        }

        var pending = new PendingRecording
        {
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
            CaptureDirectory = _configService.CaptureValidationFixtures.Value ? Path.Combine(BepInEx.Paths.CachePath, "GTR-Validation") : null,
            LevelSource = _configService.CaptureValidationFixtures.Value
                ? (currentLevel.useLevelV15Data ? currentLevel.GetV15LevelData() : string.Join("\n", currentLevel.GetOldLevelData())) : null,
            Baseline = _configService.CaptureValidationFixtures.Value ? null : _recordFeedbackService.CaptureBaseline()
        };
        return pending;
    }

    private static Task<PreparedRecording> EncodeRecording(PendingRecording pending, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using LimitedMemoryStream stream = new(GhostLimits.MaxCompressedBytes);
        var encoding = System.Diagnostics.Stopwatch.StartNew();
        pending.Snapshot.Write(stream);
        encoding.Stop();
        token.ThrowIfCancellationRequested();
        pending.Resource.GhostData = Convert.ToBase64String(stream.GetBuffer(), 0, checked((int)stream.Length));
        string json = JsonConvert.SerializeObject(pending.Resource);
        bool captured = pending.CaptureDirectory != null;
        if (captured)
            ValidationFixtureWriter.Write(pending.CaptureDirectory, pending.Snapshot.RunUuid, json,
                pending.LevelSource, pending.Snapshot.Measurements, encoding.Elapsed.TotalMilliseconds, stream.Length);
        return Task.FromResult(new PreparedRecording
        {
            Json = json,
            Captured = captured,
            Time = pending.Resource.Time,
            Baseline = pending.Baseline
        });
    }

    private async Task UploadRecording(PreparedRecording recording, CancellationToken token)
    {
        if (recording.Captured)
        {
            await UniTask.SwitchToMainThread(cancellationToken: token);
            _messengerService.LogSuccess("Calibration fixture saved in BepInEx/cache/GTR-Validation");
            return;
        }

        // Prepared JSON retains same V8 run UUID and ghost bytes across every retry.
        for (int attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = await _apiHttpClient.PostJsonAsync("record/submit", recording.Json, token);
            }
            catch (HttpRequestException) when (attempt < 3)
            {
                await Task.Delay(RecordingSubmissionRetry.DelayMilliseconds(attempt), token);
                continue;
            }

            using (response)
            {
                if (attempt < 3 && RecordingSubmissionRetry.ShouldRetry((int)response.StatusCode))
                {
                    await Task.Delay(RecordingSubmissionRetry.DelayMilliseconds(attempt), token);
                    continue;
                }

                // Keep HTTP rejection diagnostics outside the transport-error retry catch.
                await ApiResponseErrors.EnsureSuccessWithBodyAsync(response);
                break;
            }
        }
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
