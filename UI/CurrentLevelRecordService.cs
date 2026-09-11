using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Steamworks;
using StrawberryShake;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.GraphQL;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.UI;

public sealed class CurrentLevelRecordService : IEagerService, IDisposable
{
    private readonly IGtrClient _gtrClient;
    private readonly ILogger<CurrentLevelRecordService> _logger;
    private readonly CurrentLevelRecordIdCache _idCache = new();

    private IDisposable _subscription;
    private CancellationTokenSource _resolutionCancellationTokenSource;
    private int _generation;
    private LevelGraphqlIdentity _level;
    private string _steamId;
    private bool _resolving;

    public CurrentLevelRecordService(IGtrClient gtrClient, ILogger<CurrentLevelRecordService> logger)
    {
        _gtrClient = gtrClient;
        _logger = logger;

        RacingApi.LevelLoaded += Restart;
        RacingApi.PlayerSpawned += Restart;
        RacingApi.Quit += Stop;
        MultiplayerApi.DisconnectedFromGame += Stop;
    }

    public CurrentLevelRecordSnapshot Snapshot { get; private set; }

    public event Action<CurrentLevelRecordSnapshot> SnapshotChanged;

    private void Restart()
    {
        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        string steamId = SteamClient.SteamId.ToString();
        if (level.IsAvailable &&
            string.Equals(_level.CacheKey, level.CacheKey, StringComparison.Ordinal) &&
            string.Equals(_steamId, steamId, StringComparison.Ordinal) &&
            (_subscription != null || _resolving))
        {
            return;
        }

        bool levelChanged = !string.Equals(_level.CacheKey, level.CacheKey, StringComparison.Ordinal);
        StopCore();
        if (levelChanged)
            Snapshot = null;

        _level = level;
        _steamId = steamId;
        if (!_level.IsAvailable)
        {
            _logger.LogWarning("Unable to start current-level record stream without level identity");
            return;
        }

        int generation = ++_generation;
        if (_idCache.TryGet(_level.CacheKey, steamId, out CurrentLevelRecordIds ids))
        {
            StartSubscription(ids, _level.CacheKey, generation);
            return;
        }

        _resolutionCancellationTokenSource = new CancellationTokenSource();
        _resolving = true;
        ResolveAndStart(_level, steamId, generation, _resolutionCancellationTokenSource).Forget();
    }

    private async UniTaskVoid ResolveAndStart(
        LevelGraphqlIdentity level,
        string steamId,
        int generation,
        CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            IOperationResult<IResolveCurrentLevelRecordIdsResult> result =
                await _gtrClient.ResolveCurrentLevelRecordIds.ExecuteAsync(
                    level.XxHash,
                    level.Hash,
                    steamId,
                    cancellationTokenSource.Token);
            result.EnsureNoErrors();

            int? levelId = result.Data?.Levels?.Nodes.FirstOrDefault()?.Id;
            if (!levelId.HasValue)
            {
                _logger.LogWarning("Unable to resolve current level GraphQL ID");
                return;
            }

            int userId = result.Data?.UserBySteamId?.Id ?? CurrentLevelRecordIdCache.MissingUserId;
            CurrentLevelRecordIds ids = new(levelId.Value, userId);
            await UniTask.SwitchToMainThread();
            if (generation != _generation ||
                !string.Equals(level.CacheKey, _level.CacheKey, StringComparison.Ordinal) ||
                !string.Equals(steamId, _steamId, StringComparison.Ordinal))
            {
                return;
            }

            _idCache.Set(level.CacheKey, steamId, ids);
            StartSubscription(ids, level.CacheKey, generation);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to resolve current-level record IDs");
        }
        finally
        {
            if (ReferenceEquals(_resolutionCancellationTokenSource, cancellationTokenSource))
            {
                _resolutionCancellationTokenSource = null;
                _resolving = false;
                cancellationTokenSource.Dispose();
            }
        }
    }

    private void StartSubscription(CurrentLevelRecordIds ids, string levelKey, int generation)
    {
        if (generation != _generation)
            return;

        _subscription = _gtrClient.WatchCurrentLevelRecords
            .Watch(ids.LevelId, ids.UserId)
            .Subscribe(new OperationObserver<IOperationResult<IWatchCurrentLevelRecordsResult>>(
                result => OnSubscriptionResult(result, levelKey, generation).Forget(),
                error => _logger.LogWarning(error, "Current-level record subscription failed")));
    }

    private async UniTaskVoid OnSubscriptionResult(
        IOperationResult<IWatchCurrentLevelRecordsResult> result,
        string levelKey,
        int generation)
    {
        try
        {
            result.EnsureNoErrors();
            CurrentLevelRecordSnapshot snapshot = Map(result.Data?.Query, levelKey);
            await UniTask.SwitchToMainThread();
            if (generation != _generation)
                return;

            Snapshot = snapshot;
            SnapshotChanged?.Invoke(snapshot);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to process current-level record subscription snapshot");
        }
    }

    private static CurrentLevelRecordSnapshot Map(IWatchCurrentLevelRecords_Query data, string levelKey)
    {
        IWatchCurrentLevelRecords_Query_PersonalBest_Nodes personalBest =
            data?.PersonalBest?.Nodes.FirstOrDefault();
        IWatchCurrentLevelRecords_Query_WorldRecord_Nodes worldRecord =
            data?.WorldRecord?.Nodes.FirstOrDefault();

        return new CurrentLevelRecordSnapshot
        {
            LevelKey = levelKey,
            PersonalBest = personalBest?.Time.HasValue != true
                ? null
                : new PersonalBestHolder
                {
                    Time = personalBest.Time.Value,
                    Rank = personalBest.LevelPosition,
                    LevelDecayedPoints = personalBest.LevelDecayedPoints,
                    PlayerDecayedPoints = personalBest.PlayerDecayedPoints
                },
            WorldRecord = worldRecord?.Time.HasValue != true
                ? null
                : new WorldRecordHolder
                {
                    Time = worldRecord.Time.Value,
                    Rank = worldRecord.LevelPosition,
                    SteamId = worldRecord.UserSteamId,
                    SteamName = worldRecord.UserName,
                    LevelDecayedPoints = worldRecord.LevelDecayedPoints,
                    PlayerDecayedPoints = worldRecord.PlayerDecayedPoints
                }
        };
    }

    private void Stop()
    {
        StopCore();
        _level = LevelGraphqlIdentity.Unavailable;
        _steamId = null;
        Snapshot = null;
    }

    private void StopCore()
    {
        _generation++;
        _subscription?.Dispose();
        _subscription = null;
        _resolutionCancellationTokenSource?.Cancel();
        _resolutionCancellationTokenSource?.Dispose();
        _resolutionCancellationTokenSource = null;
        _resolving = false;
    }

    public void Dispose()
    {
        RacingApi.LevelLoaded -= Restart;
        RacingApi.PlayerSpawned -= Restart;
        RacingApi.Quit -= Stop;
        MultiplayerApi.DisconnectedFromGame -= Stop;
        Stop();
    }
}
