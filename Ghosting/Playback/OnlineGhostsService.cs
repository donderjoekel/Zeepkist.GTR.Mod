using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.GraphQL;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.PlayerLoop;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class OnlineGhostsService : IEagerService, IDisposable
{
    private readonly ILogger<OnlineGhostsService> _logger;
    private readonly OnlineGhostGraphqlService _graphqlService;
    private readonly GhostRepository _ghostRepository;
    private readonly GhostPlayer _ghostPlayer;
    private readonly ConfigService _configService;
    private readonly MessengerService _messengerService;

    private CancellationTokenSource _cts;
    private string _levelKey;
    private readonly GhostLoadDispatcher _dispatcher;
    private readonly PlayerLoopService _playerLoop;
    private readonly PlayerLoopSubscription _update;

    public OnlineGhostsService(
        ILogger<OnlineGhostsService> logger,
        GhostRepository ghostRepository,
        GhostPlayer ghostPlayer,
        ConfigService configService,
        PlayerLoopService playerLoopService,
        MessengerService messengerService,
        OnlineGhostGraphqlService graphqlService,
        GhostLoadDispatcher dispatcher)
    {
        _logger = logger;
        _ghostRepository = ghostRepository;
        _ghostPlayer = ghostPlayer;
        _configService = configService;
        _messengerService = messengerService;
        _graphqlService = graphqlService;
        _dispatcher = dispatcher;
        _playerLoop = playerLoopService;
        _update = playerLoopService.SubscribeUpdate(OnUpdate);

        RacingApi.PlayerSpawned += OnPlayerSpawned;
        RacingApi.LevelLoaded += OnLevelLoaded;
        RacingApi.Quit += OnDisconnectedFromGame;
        MultiplayerApi.DisconnectedFromGame += OnDisconnectedFromGame;
    }

    private void OnUpdate()
    {
        // TODO: Move this to a separate service
        if (Input.GetKeyDown(_configService.ToggleEnableGhosts.Value))
        {
            _configService.EnableGhosts.Value = !_configService.EnableGhosts.Value;

            if (_configService.EnableGhosts.Value)
            {
                _messengerService.Log("Ghosts enabled");
            }
            else
            {
                _messengerService.Log("Ghosts disabled");
            }
        }
    }

    private void OnDisconnectedFromGame()
    {
        CancelLoad();
        _levelKey = null;
        _ghostPlayer.ClearGhosts();
    }

    protected virtual void OnPlayerSpawned()
    {
        if (!MultiplayerApi.IsPlayingOnline)
        {
            CancelLoad();
            _levelKey = null;
            return;
        }
        OnLevelLoaded();

        if (_configService.EnableGhosts.Value)
        {
            LoadPersonalBests();
        }
    }

    private void OnLevelLoaded()
    {
        if (!MultiplayerApi.IsPlayingOnline)
            return;
        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        if (_levelKey == level.CacheKey)
            return;
        CancelLoad();
        _ghostPlayer.ClearGhosts();
        _levelKey = level.CacheKey;
    }

    private void LoadPersonalBests()
    {
        CancelLoad();
        _cts = new CancellationTokenSource();
        LoadPersonalBestAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid LoadPersonalBestAsync(CancellationToken ct)
    {
        _logger.LogInformation("Loading personal best...");

        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        if (!level.IsAvailable)
            return;
        if (_levelKey != level.CacheKey)
        {
            _ghostPlayer.ClearGhosts();
            _levelKey = level.CacheKey;
        }

        Result<IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes>> result =
            await _graphqlService.GetPersonalBests(level, ct);

        if (ct.IsCancellationRequested)
            return;

        if (result.IsFailed)
        {
            _logger.LogError("Failed to load personal best: {Result}", result.ToString());
            return;
        }

        await UniTask.SwitchToMainThread();
        if (ct.IsCancellationRequested)
            return;
        IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes> personalBests = result.Value;

        IReadOnlyList<int> loadedGhostIds = _ghostPlayer.GetLoadedGhostIds();

        foreach (int loadedGhostId in loadedGhostIds)
        {
            if (personalBests.All(x => x.Record.Id != loadedGhostId))
            {
                try { await _dispatcher.EnqueueAsync(() => _ghostPlayer.RemoveGhost(loadedGhostId), ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            }
        }

        await UniTask.SwitchToMainThread();
        if (ct.IsCancellationRequested)
            return;
        var missing = personalBests.Where(record => !_ghostPlayer.HasGhost(record.Record.Id)).ToArray();
        try
        {
            await BoundedAsync.ForEachAsync(missing, 15,
                (record, token) => LoadGhost(record, token).AsTask(), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async UniTask LoadGhost(
        IGetPersonalBestGhosts_PersonalBestGlobals_Nodes personalBest,
        CancellationToken cancellationToken)
    {
        try
        {
            using var source = await _ghostRepository.PreloadGhostAsync(
                personalBest.Record.Id, personalBest.Record.RecordMedia.GhostUrl, cancellationToken);
            await _dispatcher.PrepareAsync(async token =>
            {
                Result<IGhost> ghost = await source.Value.GetGhost(token);
                token.ThrowIfCancellationRequested();
                if (ghost.IsFailed)
                {
                    _logger.LogWarning("Unable to load ghost {RecordId}: {Result}", personalBest.Record.Id, ghost);
                    return () => { };
                }
                return () => _ghostPlayer.AddGhost(
                    GhostType.Global, personalBest.Record.Id, personalBest.Record.User.SteamName, ghost.Value);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error) { _logger.LogWarning(error, "Unable to load ghost {RecordId}", personalBest.Record.Id); }
    }

    private void CancelLoad()
    {
        CancellationTokenSource cts = _cts;
        _cts = null;
        if (cts == null)
            return;
        cts.Cancel();
        _dispatcher.DiscardCancelled();
        cts.Dispose();
    }
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RacingApi.PlayerSpawned -= OnPlayerSpawned;
        RacingApi.LevelLoaded -= OnLevelLoaded;
        RacingApi.Quit -= OnDisconnectedFromGame;
        MultiplayerApi.DisconnectedFromGame -= OnDisconnectedFromGame;
        _playerLoop.UnsubscribeUpdate(_update);
        CancelLoad();
    }
}
