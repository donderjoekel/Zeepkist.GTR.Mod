using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.GraphQL;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.PlayerLoop;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Level;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class OfflineGhostsService : IEagerService
{
    private readonly ILogger<OfflineGhostsService> _logger;
    private readonly OfflineGhostGraphqlService _graphqlService;
    private readonly GhostRepository _ghostRepository;
    private readonly GhostPlayer _ghostPlayer;
    private readonly ConfigService _configService;
    private readonly BulkGhostModeState _bulkModeState;
    private readonly MessengerService _messengerService;

    private readonly List<string> _additionalGhosts = new();
    private readonly HashSet<int> _bulkGhostIds = new();
    private readonly ConcurrentQueue<PendingGhostOperation> _pendingOperations = new();

    private CancellationTokenSource _cts;
    private string _bulkLevelCacheKey;
    private string _loadedLevelCacheKey;
    private int _loadGeneration;
    private int _progressGeneration;
    private int _progressTotal;
    private int _progressCompleted;
    private int _progressLoaded;
    private int _lastReportedProgressCompleted;
    private bool _progressActive;

    public bool IsShowingAllGhosts { get; private set; }
    public bool IsShowingTopRecords { get; private set; }
    public int TopRecordLimit => _configService.MaximumVisibleTopRecordGhosts.Value;
    public event Action BulkModeChanged;

    public OfflineGhostsService(
        ILogger<OfflineGhostsService> logger,
        OfflineGhostGraphqlService graphqlService,
        GhostRepository ghostRepository,
        GhostPlayer ghostPlayer,
        ConfigService configService,
        PlayerLoopService playerLoopService,
        MessengerService messengerService,
        BulkGhostModeState bulkModeState)
    {
        _logger = logger;
        _graphqlService = graphqlService;
        _ghostRepository = ghostRepository;
        _ghostPlayer = ghostPlayer;
        _configService = configService;
        _messengerService = messengerService;
        _bulkModeState = bulkModeState;

        playerLoopService.SubscribeUpdate(OnUpdate);
        RacingApi.PlayerSpawned += OnPlayerSpawned;
        RacingApi.RoundEnded += OnRoundEnded;
        RacingApi.Quit += OnQuit;
    }

    private void OnUpdate()
    {
        DrainPendingOperations();
        UpdateLoadProgress();

        if (MultiplayerApi.IsPlayingOnline)
            return;
        if (PlayerManager.Instance == null ||
            PlayerManager.Instance.currentMaster == null ||
            PlayerManager.Instance.currentMaster.OnlineGameplayUI == null)
        {
            return;
        }

        if (PlayerManager.Instance.currentMaster.OnlineGameplayUI.LeaderboardAction.buttonDown)
            PlayerManager.Instance.currentMaster.OnlineGameplayUI.OnlineTabLeaderboard.Open(true);
    }

    protected virtual void OnPlayerSpawned()
    {
        if (MultiplayerApi.IsPlayingOnline || !_configService.EnableGhosts.Value)
            return;

        LevelGraphqlIdentity level = PrepareLevel();
        if (!level.IsAvailable)
            return;

        if ((IsShowingAllGhosts || IsShowingTopRecords) && _bulkLevelCacheKey == level.CacheKey)
            LoadBulkGhosts();
        else
            LoadGhosts();
    }

    private void OnQuit()
    {
        if (MultiplayerApi.IsPlayingOnline)
            return;

        CancelLoad();
        SetBulkMode(false, false);
        _loadedLevelCacheKey = null;
        _bulkLevelCacheKey = null;
        _bulkGhostIds.Clear();
        _ghostPlayer.ClearGhosts();
    }

    private static void OnRoundEnded()
    {
        // Keep same-level ghosts loaded. GhostPlayer resets playback state.
    }

    private void LoadGhosts()
    {
        (CancellationToken token, int generation) = BeginLoad();
        LoadGhostsAsync(token, generation).Forget();
    }

    private async UniTaskVoid LoadGhostsAsync(CancellationToken ct, int generation)
    {
        (
            Result<IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes>> pbResult,
            Result<IReadOnlyList<IGetAdditionalGhosts_PersonalBestGlobals_Nodes>> additionalResult
        ) = await UniTask.WhenAll(
            LoadPersonalBestsAsync(ct),
            LoadAdditionalGhostsAsync(ct));

        if (ct.IsCancellationRequested ||
            !GhostLoadBudget.IsCurrentGeneration(generation, Volatile.Read(ref _loadGeneration)))
            return;

        var records = new List<IGhostRecordFrag>();
        if (pbResult.IsSuccess)
            records.AddRange(pbResult.Value.Select(x => x.Record));
        else
            _logger.LogWarning("Loading Personal Bests failed: {Result}", pbResult);

        if (additionalResult.IsSuccess)
            records.AddRange(additionalResult.Value.Select(x => x.Record));
        else
            _logger.LogWarning("Loading AdditionalGhosts failed: {Result}", additionalResult);

        List<IGhostRecordFrag> distinctRecords = records
            .GroupBy(record => record.Id)
            .Select(group => group.First())
            .ToList();
        var desiredIds = distinctRecords.Select(record => record.Id).ToHashSet();

        await LoadRecords(distinctRecords, GhostVisualProfile.Full, ct, generation);
        EnqueueReconciliation(desiredIds, generation);
    }

    public void ShowAllGhosts()
    {
        if (IsShowingAllGhosts)
            return;

        LevelGraphqlIdentity level = PrepareLevel();
        if (!level.IsAvailable)
            return;

        _bulkLevelCacheKey = level.CacheKey;
        SetBulkMode(true, false);
        LoadBulkGhosts();
    }

    public void ClearAllGhosts()
    {
        CancelLoad();
        _bulkGhostIds.Clear();
        _bulkLevelCacheKey = null;
        SetBulkMode(false, IsShowingTopRecords);
        _ghostPlayer.ClearGhosts();

        if (_configService.EnableGhosts.Value && CurrentLevelGraphqlIdentity.Create().IsAvailable)
            LoadCurrentMode();
    }

    public void ShowTopRecords()
    {
        if (IsShowingTopRecords)
            return;

        LevelGraphqlIdentity level = PrepareLevel();
        if (!level.IsAvailable)
            return;

        _bulkLevelCacheKey = level.CacheKey;
        SetBulkMode(false, true);
        LoadBulkGhosts();
    }

    public void ClearTopRecords()
    {
        CancelLoad();
        _bulkGhostIds.Clear();
        _bulkLevelCacheKey = null;
        SetBulkMode(IsShowingAllGhosts, false);
        _ghostPlayer.ClearGhosts();

        if (_configService.EnableGhosts.Value && CurrentLevelGraphqlIdentity.Create().IsAvailable)
            LoadCurrentMode();
    }

    private void LoadCurrentMode()
    {
        if (IsShowingAllGhosts || IsShowingTopRecords)
            LoadBulkGhosts();
        else
            LoadGhosts();
    }

    private void LoadBulkGhosts()
    {
        (CancellationToken token, int generation) = BeginLoad();
        LoadBulkGhostsAsync(token, generation).Forget();
    }

    private async UniTaskVoid LoadBulkGhostsAsync(CancellationToken ct, int generation)
    {
        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        if (!level.IsAvailable)
            return;

        int? first = IsShowingTopRecords
            ? _configService.MaximumVisibleTopRecordGhosts.Value
            : OfflineGhostLimit.ToGraphQlFirst(_configService.MaximumVisibleOfflineGhosts.Value);
        (
            Result<IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes>> pbResult,
            Result<IReadOnlyList<IGetAdditionalGhosts_PersonalBestGlobals_Nodes>> additionalResult,
            Result<IReadOnlyList<IGhostRecordFrag>> bulkResult
        ) = await UniTask.WhenAll(
            LoadPersonalBestsAsync(level, ct),
            LoadAdditionalGhostsAsync(level, ct),
            LoadBulkRecordsAsync(level, first, ct));

        if (ct.IsCancellationRequested ||
            !GhostLoadBudget.IsCurrentGeneration(generation, Volatile.Read(ref _loadGeneration)) ||
            !(IsShowingAllGhosts || IsShowingTopRecords))
        {
            return;
        }

        var protectedGhosts = new List<IGhostRecordFrag>();
        if (pbResult.IsSuccess)
            protectedGhosts.AddRange(pbResult.Value.Select(x => x.Record));
        else
            _logger.LogWarning("Loading Personal Bests failed: {Result}", pbResult);

        if (additionalResult.IsSuccess)
            protectedGhosts.AddRange(additionalResult.Value.Select(x => x.Record));
        else
            _logger.LogWarning("Loading AdditionalGhosts failed: {Result}", additionalResult);

        IReadOnlyList<IGhostRecordFrag> bulkGhosts = [];
        if (bulkResult.IsSuccess)
            bulkGhosts = bulkResult.Value;
        else
            _logger.LogWarning("Loading bulk ghost records failed: {Result}", bulkResult);

        _bulkGhostIds.Clear();
        foreach (IGhostRecordFrag record in bulkGhosts)
            _bulkGhostIds.Add(record.Id);

        List<IGhostRecordFrag> distinctProtected = protectedGhosts
            .GroupBy(record => record.Id)
            .Select(group => group.First())
            .ToList();
        HashSet<int> protectedIds = distinctProtected.Select(record => record.Id).ToHashSet();

        var distinctBulk = bulkGhosts
            .Where(record => !protectedIds.Contains(record.Id))
            .GroupBy(record => record.Id)
            .Select(group => group.First())
            .ToList();

        var desiredIds = protectedIds.ToHashSet();
        desiredIds.UnionWith(_bulkGhostIds);

        BeginProgress(generation, distinctProtected.Count + distinctBulk.Count);
        await LoadRecords(distinctProtected, GhostVisualProfile.Full, ct, generation);
        await LoadRecords(distinctBulk, GhostVisualProfile.Bulk, ct, generation);
        EnqueueReconciliation(desiredIds, generation);
    }

    private async UniTask<Result<IReadOnlyList<IGhostRecordFrag>>> LoadBulkRecordsAsync(
        LevelGraphqlIdentity level,
        int? first,
        CancellationToken ct)
    {
        if (IsShowingTopRecords)
        {
            Result<IReadOnlyList<IGetTopRecordGhosts_Records_Nodes>> result =
                await _graphqlService.GetTopRecordGhosts(
                    level,
                    first.GetValueOrDefault(_configService.MaximumVisibleTopRecordGhosts.Value),
                    ct);
            return result.IsSuccess
                ? Result.Ok<IReadOnlyList<IGhostRecordFrag>>(result.Value.Select(record => (IGhostRecordFrag)record).ToList())
                : Result.Fail<IReadOnlyList<IGhostRecordFrag>>(result.Errors);
        }

        Result<IReadOnlyList<IGetAllPersonalBestGhosts_Records_Nodes>> pbResult =
            await _graphqlService.GetAllPersonalBestGhosts(level, first, ct);
        return pbResult.IsSuccess
            ? Result.Ok<IReadOnlyList<IGhostRecordFrag>>(pbResult.Value.Select(record => (IGhostRecordFrag)record).ToList())
            : Result.Fail<IReadOnlyList<IGhostRecordFrag>>(pbResult.Errors);
    }

    private async UniTask<Result<IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes>>>
        LoadPersonalBestsAsync(CancellationToken ct)
    {
        return await LoadPersonalBestsAsync(CurrentLevelGraphqlIdentity.Create(), ct);
    }

    private async UniTask<Result<IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes>>>
        LoadPersonalBestsAsync(LevelGraphqlIdentity level, CancellationToken ct)
    {
        if (!level.IsAvailable)
            return Result.Ok<IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes>>([]);

        Result<IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes>> result =
            await _graphqlService.GetPersonalBests(level, ct);

        if (ct.IsCancellationRequested)
            return Result.Ok();
        if (result.IsFailed)
            _logger.LogError("Failed to load personal bests: {Result}", result);
        return result;
    }

    private async UniTask<Result<IReadOnlyList<IGetAdditionalGhosts_PersonalBestGlobals_Nodes>>>
        LoadAdditionalGhostsAsync(CancellationToken ct)
    {
        return await LoadAdditionalGhostsAsync(CurrentLevelGraphqlIdentity.Create(), ct);
    }

    private async UniTask<Result<IReadOnlyList<IGetAdditionalGhosts_PersonalBestGlobals_Nodes>>>
        LoadAdditionalGhostsAsync(LevelGraphqlIdentity level, CancellationToken ct)
    {
        if (!level.IsAvailable)
            return Result.Ok<IReadOnlyList<IGetAdditionalGhosts_PersonalBestGlobals_Nodes>>([]);

        Result<IReadOnlyList<IGetAdditionalGhosts_PersonalBestGlobals_Nodes>> result =
            await _graphqlService.GetAdditionalGhosts(_additionalGhosts, level, ct);

        if (ct.IsCancellationRequested)
            return Result.Ok();
        if (result.IsFailed)
            _logger.LogError("Failed to load additional ghosts: {Result}", result);
        return result;
    }

    public void AddAdditionalGhost(string steamId)
    {
        if (!_additionalGhosts.Contains(steamId))
            _additionalGhosts.Add(steamId);
        LoadCurrentMode();
    }

    public bool ContainsAdditionalGhost(string steamId) => _additionalGhosts.Contains(steamId);

    public void RemoveAdditionalGhost(string steamId)
    {
        _additionalGhosts.Remove(steamId);
        LoadCurrentMode();
    }

    private async UniTask LoadRecords(
        IReadOnlyCollection<IGhostRecordFrag> records,
        GhostVisualProfile visualProfile,
        CancellationToken cancellationToken,
        int generation)
    {
        IEnumerable<UniTask> loads = records.Select(record =>
            LoadGhost(record, cancellationToken, visualProfile, generation));
        await UniTask.WhenAll(loads);
    }

    private async UniTask LoadGhost(
        IGhostRecordFrag record,
        CancellationToken cancellationToken,
        GhostVisualProfile visualProfile,
        int generation)
    {
        if (_ghostPlayer.HasGhost(record.Id, visualProfile))
        {
            CompleteProgressRecord(generation, true);
            return;
        }

        Result<IGhost> ghost;
        try
        {
            ghost = await _ghostRepository.GetGhost(
                record.Id,
                record.RecordMedia.GhostUrl,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to download or parse ghost {RecordId}",
                record.Id);
            CompleteProgressRecord(generation, false);
            return;
        }

        if (cancellationToken.IsCancellationRequested ||
            !GhostLoadBudget.IsCurrentGeneration(generation, Volatile.Read(ref _loadGeneration)))
        {
            return;
        }

        if (ghost.IsFailed)
        {
            _logger.LogError("Unable to get ghost from repository: {Result}", ghost.ToString());
            CompleteProgressRecord(generation, false);
            return;
        }

        _pendingOperations.Enqueue(PendingGhostOperation.Add(
            generation,
            record.Id,
            record.User.SteamName,
            ghost.Value,
            visualProfile));
    }

    private void SetBulkMode(bool showAllGhosts, bool showTopRecords)
    {
        bool wasActive = IsShowingAllGhosts || IsShowingTopRecords;
        bool isActive = showAllGhosts || showTopRecords;
        if (IsShowingAllGhosts == showAllGhosts && IsShowingTopRecords == showTopRecords)
            return;

        IsShowingAllGhosts = showAllGhosts;
        IsShowingTopRecords = showTopRecords;
        _bulkModeState.SetActive(isActive);
        if (wasActive != isActive || showAllGhosts || showTopRecords)
            BulkModeChanged?.Invoke();
    }

    private (CancellationToken Token, int Generation) BeginLoad()
    {
        CancelLoad();
        _cts = new CancellationTokenSource();
        return (_cts.Token, Volatile.Read(ref _loadGeneration));
    }

    private void CancelLoad()
    {
        Interlocked.Increment(ref _loadGeneration);
        _progressActive = false;
        while (_pendingOperations.TryDequeue(out _))
        {
        }

        CancellationTokenSource cts = _cts;
        _cts = null;
        if (cts == null)
            return;

        cts.Cancel();
        cts.Dispose();
    }

    private LevelGraphqlIdentity PrepareLevel()
    {
        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        if (!level.IsAvailable)
            return level;

        if (_loadedLevelCacheKey == level.CacheKey)
            return level;

        CancelLoad();
        _ghostPlayer.ClearGhosts();
        _loadedLevelCacheKey = level.CacheKey;
        return level;
    }

    private void EnqueueReconciliation(HashSet<int> desiredIds, int generation)
    {
        if (GhostLoadBudget.IsCurrentGeneration(generation, Volatile.Read(ref _loadGeneration)))
            _pendingOperations.Enqueue(PendingGhostOperation.Reconcile(generation, desiredIds));
    }

    private void DrainPendingOperations()
    {
        int processed = 0;
        long startedAt = Stopwatch.GetTimestamp();
        while (GhostLoadBudget.CanProcessNext(processed, GetElapsedMilliseconds(startedAt)) &&
               _pendingOperations.TryDequeue(out PendingGhostOperation operation))
        {
            if (!GhostLoadBudget.IsCurrentGeneration(
                    operation.Generation,
                    Volatile.Read(ref _loadGeneration)))
            {
                continue;
            }

            if (operation.DesiredIds != null)
            {
                IReadOnlyList<int> obsoleteIds = GhostReconciliation.GetObsoleteIds(
                    _ghostPlayer.GetLoadedGhostIds(),
                    operation.DesiredIds);
                foreach (int loadedGhostId in obsoleteIds)
                {
                    _pendingOperations.Enqueue(
                        PendingGhostOperation.Remove(operation.Generation, loadedGhostId));
                }
            }
            else if (operation.RemoveRecordId.HasValue)
            {
                _ghostPlayer.RemoveGhost(operation.RemoveRecordId.Value);
            }
            else
            {
                try
                {
                    _ghostPlayer.AddGhost(
                        GhostType.Global,
                        operation.RecordId,
                        operation.SteamName,
                        operation.Ghost,
                        operation.VisualProfile);
                    CompleteProgressRecord(operation.Generation, true);
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Unable to add ghost {RecordId}",
                        operation.RecordId);
                    CompleteProgressRecord(operation.Generation, false);
                }
            }

            processed++;
        }
    }

    private static double GetElapsedMilliseconds(long startedAt)
    {
        return (Stopwatch.GetTimestamp() - startedAt) * 1000d / Stopwatch.Frequency;
    }

    private void BeginProgress(int generation, int total)
    {
        Volatile.Write(ref _progressGeneration, generation);
        Volatile.Write(ref _progressTotal, total);
        Volatile.Write(ref _progressCompleted, 0);
        Volatile.Write(ref _progressLoaded, 0);
        _lastReportedProgressCompleted = 0;
        _progressActive = true;
    }

    private void CompleteProgressRecord(int generation, bool loaded)
    {
        if (!GhostLoadBudget.IsCurrentGeneration(
                generation,
                Volatile.Read(ref _progressGeneration)))
        {
            return;
        }

        if (loaded)
            Interlocked.Increment(ref _progressLoaded);
        Interlocked.Increment(ref _progressCompleted);
    }

    private void UpdateLoadProgress()
    {
        if (!_progressActive)
            return;

        int total = Volatile.Read(ref _progressTotal);
        int completed = Volatile.Read(ref _progressCompleted);
        if (!GhostLoadProgress.HasAdvanced(completed, _lastReportedProgressCompleted))
            return;

        _lastReportedProgressCompleted = completed;
        int percent = GhostLoadProgress.CalculatePercent(completed, total);

        int loaded = Volatile.Read(ref _progressLoaded);
        if (percent < 100)
        {
            _messengerService.Log(
                $"Loading ghosts: {percent}% ({loaded}/{total})",
                1.5f);
            return;
        }

        _progressActive = false;
        if (loaded == total)
        {
            _messengerService.LogSuccess(
                $"Loading ghosts: 100% ({loaded}/{total})",
                2);
        }
        else
        {
            _messengerService.LogWarning(
                $"Loading ghosts: 100% ({loaded}/{total})",
                2);
        }
    }

    private sealed class PendingGhostOperation
    {
        public int Generation { get; private set; }
        public int RecordId { get; private set; }
        public string SteamName { get; private set; }
        public IGhost Ghost { get; private set; }
        public GhostVisualProfile VisualProfile { get; private set; }
        public HashSet<int> DesiredIds { get; private set; }
        public int? RemoveRecordId { get; private set; }

        public static PendingGhostOperation Add(
            int generation,
            int recordId,
            string steamName,
            IGhost ghost,
            GhostVisualProfile visualProfile)
        {
            return new PendingGhostOperation
            {
                Generation = generation,
                RecordId = recordId,
                SteamName = steamName,
                Ghost = ghost,
                VisualProfile = visualProfile
            };
        }

        public static PendingGhostOperation Reconcile(int generation, HashSet<int> desiredIds)
        {
            return new PendingGhostOperation
            {
                Generation = generation,
                DesiredIds = desiredIds
            };
        }

        public static PendingGhostOperation Remove(int generation, int recordId)
        {
            return new PendingGhostOperation
            {
                Generation = generation,
                RemoveRecordId = recordId
            };
        }
    }
}
