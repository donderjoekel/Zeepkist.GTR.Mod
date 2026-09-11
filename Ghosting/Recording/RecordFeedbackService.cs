using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Steamworks;
using StrawberryShake;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.GraphQL;
using TNRD.Zeepkist.GTR.UI;
using ZeepSDK.Chat;
using ZeepSDK.Extensions;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public sealed class RecordFeedbackBaseline
{
    public string LevelKey { get; set; }
    public double? PersonalBestTime { get; set; }
    public int? PersonalBestPosition { get; set; }
    public double? WorldRecordTime { get; set; }
    public string WorldRecordSteamId { get; set; }
}

public sealed class RecordFeedbackService : IEagerService, IDisposable
{
    private readonly CurrentLevelRecordService _currentLevelRecordService;
    private readonly ConfigService _configService;
    private readonly IGtrClient _gtrClient;
    private readonly ILogger<RecordFeedbackService> _logger;

    private CancellationTokenSource _pendingCancellationTokenSource;
    private PendingFeedback _pending;
    private int _generation;

    public RecordFeedbackService(
        CurrentLevelRecordService currentLevelRecordService,
        ConfigService configService,
        IGtrClient gtrClient,
        ILogger<RecordFeedbackService> logger)
    {
        _currentLevelRecordService = currentLevelRecordService;
        _configService = configService;
        _gtrClient = gtrClient;
        _logger = logger;
        _currentLevelRecordService.SnapshotChanged += OnSnapshotChanged;
        RacingApi.LevelLoaded += CancelForLevelChange;
        RacingApi.Quit += CancelForLevelChange;
        MultiplayerApi.DisconnectedFromGame += CancelForLevelChange;
    }

    public RecordFeedbackBaseline CaptureBaseline()
    {
        CurrentLevelRecordSnapshot snapshot = _currentLevelRecordService.Snapshot;
        if (snapshot == null)
            return null;

        RecordFeedbackBaseline baseline = new()
        {
            LevelKey = snapshot.LevelKey,
            PersonalBestTime = snapshot.PersonalBest?.Time,
            PersonalBestPosition = snapshot.PersonalBest?.Rank,
            WorldRecordTime = snapshot.WorldRecord?.Time,
            WorldRecordSteamId = snapshot.WorldRecord?.SteamId
        };

        PendingFeedback pending = _pending;
        if (pending == null || !string.Equals(pending.Baseline.LevelKey, baseline.LevelKey, StringComparison.Ordinal))
            return baseline;

        if (!baseline.PersonalBestTime.HasValue || pending.SubmittedTime < baseline.PersonalBestTime.Value)
            baseline.PersonalBestTime = pending.SubmittedTime;

        if (pending.BaselineKind is RecordFeedbackKind.NewWorldRecord or RecordFeedbackKind.ImprovedWorldRecord &&
            (!baseline.WorldRecordTime.HasValue || pending.SubmittedTime < baseline.WorldRecordTime.Value))
        {
            baseline.WorldRecordTime = pending.SubmittedTime;
            baseline.WorldRecordSteamId = SteamClient.SteamId.ToString();
        }

        return baseline;
    }

    public void HandleSuccessfulSubmission(double submittedTime, RecordFeedbackBaseline baseline)
    {
        if (baseline == null)
        {
            _logger.LogWarning("Skipping record feedback because no pre-submit snapshot was available");
            return;
        }
        if (baseline.PersonalBestTime.HasValue && !baseline.PersonalBestPosition.HasValue)
        {
            _logger.LogWarning("Skipping record feedback because the pre-submit PB position was unavailable");
            return;
        }

        string playerSteamId = SteamClient.SteamId.ToString();
        RecordFeedbackKind baselineKind = RecordFeedbackFormatter.Classify(
            submittedTime,
            baseline.PersonalBestTime,
            baseline.WorldRecordTime,
            baseline.WorldRecordSteamId,
            playerSteamId);
        if (baselineKind == RecordFeedbackKind.None)
            return;

        CancelPending();
        int generation = ++_generation;
        _pending = new PendingFeedback(baseline, submittedTime, baselineKind, playerSteamId, generation);
        _pendingCancellationTokenSource = new CancellationTokenSource();
        TryComplete(_currentLevelRecordService.Snapshot).Forget();
    }

    private bool ShouldShow(RecordFeedbackKind kind)
    {
        return RecordFeedbackFormatter.ShouldShow(
            kind,
            _configService.ShowPersonalBestImprovementMessages.Value,
            _configService.ShowPersonalBestBecameWorldRecordMessages.Value,
            _configService.ShowWorldRecordImprovementMessages.Value);
    }

    private void OnSnapshotChanged(CurrentLevelRecordSnapshot snapshot)
    {
        TryComplete(snapshot).Forget();
    }

    private async UniTask TryComplete(CurrentLevelRecordSnapshot snapshot)
    {
        PendingFeedback pending = _pending;
        if (pending == null || pending.Generation != _generation)
            return;
        if (snapshot != null && !string.Equals(snapshot.LevelKey, pending.Baseline.LevelKey, StringComparison.Ordinal))
            return;

        PersonalBestHolder personalBest = snapshot?.PersonalBest;
        bool matchingPersonalBest = personalBest != null &&
                                    Math.Abs(personalBest.Time - pending.SubmittedTime) < 0.001;
        if (matchingPersonalBest && personalBest.Rank.HasValue)
        {
            bool isWorldRecord = RecordFeedbackFormatter.IsWorldRecordPosition(personalBest.Rank);
            pending.ConfirmedKind = ClassifyConfirmed(pending, isWorldRecord);
            if (!ShouldShow(pending.ConfirmedKind.Value))
            {
                CompleteWithoutMessage(pending);
                return;
            }

            if (RecordFeedbackFormatter.RequiresNextFastest(pending.ConfirmedKind.Value) &&
                !pending.NextFastestRequestStarted)
            {
                pending.NextFastestRequestStarted = true;
                LoadNextDeltaAsync(pending).Forget();
            }
        }

        RecordFeedbackKind kind = pending.ConfirmedKind ?? RecordFeedbackKind.None;
        if (kind == RecordFeedbackKind.None || !ShouldShow(kind))
            return;

        bool projectionReady = RecordFeedbackFormatter.HasRankedProjection(
            matchingPersonalBest,
            personalBest?.Rank,
            personalBest?.LevelDecayedPoints);
        bool nextFastestReady = kind is RecordFeedbackKind.NewWorldRecord or RecordFeedbackKind.ImprovedWorldRecord ||
                                pending.NextFastestRequestCompleted;
        if (!projectionReady || !nextFastestReady)
            return;

        RecordFeedbackMessageData data = new()
        {
            Kind = kind,
            PreviousDelta = FormatPreviousDelta(pending, kind),
            NextDelta = pending.NextDelta,
            WasFirstPersonalBest = !pending.Baseline.PersonalBestTime.HasValue,
            PreviousPosition = pending.Baseline.PersonalBestPosition,
            Position = matchingPersonalBest ? personalBest.Rank : null,
            LevelDecayedPoints = matchingPersonalBest ? personalBest.LevelDecayedPoints : null,
            PlayerDecayedPoints = matchingPersonalBest ? personalBest.PlayerDecayedPoints : null
        };

        CompleteWithoutMessage(pending);
        await UniTask.SwitchToMainThread();
        string message = RecordFeedbackFormatter.Format(data);
        if (!string.IsNullOrEmpty(message))
            ChatApi.AddLocalMessage(message);
    }

    private async UniTaskVoid LoadNextDeltaAsync(PendingFeedback pending)
    {
        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        if (!level.IsAvailable || !string.Equals(level.CacheKey, pending.Baseline.LevelKey, StringComparison.Ordinal))
        {
            MarkNextFastestCompleted(pending, null);
            return;
        }

        string nextDelta = null;
        try
        {
            IOperationResult<IGetNextFastestPersonalBestResult> result =
                await _gtrClient.GetNextFastestPersonalBest.ExecuteAsync(
                    level.XxHash,
                    level.Hash,
                    pending.SubmittedTime,
                    _pendingCancellationTokenSource?.Token ?? default);
            result.EnsureNoErrors();
            double? nextTime = result.Data?.Records?.Nodes.FirstOrDefault()?.Time;
            if (nextTime.HasValue && pending.SubmittedTime > nextTime.Value)
                nextDelta = (pending.SubmittedTime - nextTime.Value).GetFormattedTime();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to load next fastest PB for record feedback");
        }

        MarkNextFastestCompleted(pending, nextDelta);
    }

    private void MarkNextFastestCompleted(PendingFeedback pending, string nextDelta)
    {
        if (!ReferenceEquals(_pending, pending) || pending.Generation != _generation)
            return;

        pending.NextDelta = nextDelta;
        pending.NextFastestRequestCompleted = true;
        TryComplete(_currentLevelRecordService.Snapshot).Forget();
    }

    private static RecordFeedbackKind ClassifyConfirmed(PendingFeedback pending, bool isWorldRecord)
    {
        bool previousWorldRecordOwnedByPlayer = pending.Baseline.WorldRecordTime.HasValue &&
                                                string.Equals(
                                                    pending.Baseline.WorldRecordSteamId,
                                                    pending.PlayerSteamId,
                                                    StringComparison.Ordinal);
        return RecordFeedbackFormatter.ClassifyConfirmed(
            pending.Baseline.PersonalBestTime.HasValue,
            isWorldRecord,
            previousWorldRecordOwnedByPlayer);
    }

    private static string FormatPreviousDelta(PendingFeedback pending, RecordFeedbackKind kind)
    {
        double? previousTime = kind == RecordFeedbackKind.ImprovedWorldRecord
            ? pending.Baseline.WorldRecordTime
            : pending.Baseline.PersonalBestTime;
        return previousTime.HasValue && previousTime.Value > pending.SubmittedTime
            ? (previousTime.Value - pending.SubmittedTime).GetFormattedTime()
            : null;
    }

    private void CompleteWithoutMessage(PendingFeedback pending)
    {
        if (!ReferenceEquals(_pending, pending))
            return;

        _generation++;
        CancelPending();
    }

    private void CancelPending()
    {
        _pending = null;
        _pendingCancellationTokenSource?.Cancel();
        _pendingCancellationTokenSource?.Dispose();
        _pendingCancellationTokenSource = null;
    }

    private void CancelForLevelChange()
    {
        _generation++;
        CancelPending();
    }

    public void Dispose()
    {
        _currentLevelRecordService.SnapshotChanged -= OnSnapshotChanged;
        RacingApi.LevelLoaded -= CancelForLevelChange;
        RacingApi.Quit -= CancelForLevelChange;
        MultiplayerApi.DisconnectedFromGame -= CancelForLevelChange;
        CancelForLevelChange();
    }

    private sealed class PendingFeedback
    {
        public PendingFeedback(
            RecordFeedbackBaseline baseline,
            double submittedTime,
            RecordFeedbackKind baselineKind,
            string playerSteamId,
            int generation)
        {
            Baseline = baseline;
            SubmittedTime = submittedTime;
            BaselineKind = baselineKind;
            PlayerSteamId = playerSteamId;
            Generation = generation;
        }

        public RecordFeedbackBaseline Baseline { get; }
        public double SubmittedTime { get; }
        public RecordFeedbackKind BaselineKind { get; }
        public string PlayerSteamId { get; }
        public int Generation { get; }
        public RecordFeedbackKind? ConfirmedKind { get; set; }
        public bool NextFastestRequestStarted { get; set; }
        public bool NextFastestRequestCompleted { get; set; }
        public string NextDelta { get; set; }
    }
}
