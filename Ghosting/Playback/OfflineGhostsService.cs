using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
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
    private readonly MessengerService _messengerService;

    private readonly List<string> _additionalGhosts = new();

    private CancellationTokenSource _cts;
    private string _levelHash;

    public OfflineGhostsService(
        ILogger<OfflineGhostsService> logger,
        OfflineGhostGraphqlService graphqlService,
        GhostRepository ghostRepository,
        GhostPlayer ghostPlayer,
        ConfigService configService,
        PlayerLoopService playerLoopService,
        MessengerService messengerService)
    {
        _logger = logger;
        _graphqlService = graphqlService;
        _ghostRepository = ghostRepository;
        _ghostPlayer = ghostPlayer;
        _configService = configService;
        _messengerService = messengerService;

        playerLoopService.SubscribeUpdate(OnUpdate);

        RacingApi.LevelLoaded += OnLevelLoaded;
        RacingApi.PlayerSpawned += OnPlayerSpawned;
        RacingApi.RoundEnded += OnRoundEnded;
        RacingApi.Quit += OnQuit;
        // TODO: Figure out when quitting to main menu
        // TODO: Figure out when pause menu opened
    }

    private void OnUpdate()
    {
        if (MultiplayerApi.IsPlayingOnline)
            return;

        if (PlayerManager.Instance == null) return;
        if (PlayerManager.Instance.currentMaster == null) return;
        if (PlayerManager.Instance.currentMaster.OnlineGameplayUI == null) return;

        if (PlayerManager.Instance.currentMaster.OnlineGameplayUI.LeaderboardAction.buttonDown)
        {
            PlayerManager.Instance.currentMaster.OnlineGameplayUI.OnlineTabLeaderboard.Open(true);
        }
    }

    private void OnLevelLoaded()
    {
        if (MultiplayerApi.IsPlayingOnline)
            return;

        _levelHash = LevelApi.GetLevelHash(LevelApi.CurrentLevel);
    }

    protected virtual void OnPlayerSpawned()
    {
        if (MultiplayerApi.IsPlayingOnline)
            return;

        if (_configService.EnableGhosts.Value)
        {
            LoadGhosts();
        }
    }

    private void OnQuit()
    {
        if (MultiplayerApi.IsPlayingOnline)
            return;

        _ghostPlayer.ClearGhosts();
    }

    private void OnRoundEnded()
    {
        if (MultiplayerApi.IsPlayingOnline)
            return;

        _ghostPlayer.ClearGhosts();
    }

    private void LoadGhosts()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        LoadGhostsAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid LoadGhostsAsync(CancellationToken ct)
    {
        (Result<List<OnlineGhostGraphqlService.PersonalBest>> pbResult,
            Result<List<OnlineGhostGraphqlService.PersonalBest>> additionalResult) = await UniTask.WhenAll(
            LoadPersonalBestsAsync(ct),
            LoadAdditionalGhostsAsync(ct));

        List<OnlineGhostGraphqlService.PersonalBest> personalBests = new();

        if (pbResult.IsSuccess)
        {
            personalBests.AddRange(pbResult.Value);
        }
        else
        {
            _logger.LogWarning("Loading Personal Bests failed: {Result}", pbResult);
        }

        if (additionalResult.IsSuccess)
        {
            personalBests.AddRange(additionalResult.Value);
        }
        else
        {
            _logger.LogWarning("Loading AdditionalGhosts failed: {Result}", additionalResult);
        }

        IReadOnlyList<int> loadedGhostIds = _ghostPlayer.GetLoadedGhostIds();

        foreach (int loadedGhostId in loadedGhostIds)
        {
            if (personalBests.All(x => x.Id != loadedGhostId))
            {
                _ghostPlayer.RemoveGhost(loadedGhostId);
            }
        }

        foreach (OnlineGhostGraphqlService.PersonalBest personalBest in personalBests)
        {
            Result<IGhost> ghost = await _ghostRepository.GetGhost(personalBest.Id, personalBest.GhostUrl);
            if (ghost.IsFailed)
            {
                _logger.LogError("Unable to get ghost from repository: {Result}", ghost.ToString());
                continue;
            }

            _ghostPlayer.AddGhost(personalBest.Id, personalBest.SteamName, ghost.Value);
        }
    }

    private async UniTask<Result<List<OnlineGhostGraphqlService.PersonalBest>>> LoadPersonalBestsAsync(
        CancellationToken ct)
    {
        Result<List<OnlineGhostGraphqlService.PersonalBest>> result
            = await _graphqlService.GetPersonalBests(_levelHash);

        if (ct.IsCancellationRequested)
            return Result.Ok();

        if (result.IsFailed)
            _logger.LogError("Failed to load personal bests: {Result}", result);

        return result;
    }

    private async UniTask<Result<List<OnlineGhostGraphqlService.PersonalBest>>> LoadAdditionalGhostsAsync(
        CancellationToken ct)
    {
        Result<List<OnlineGhostGraphqlService.PersonalBest>> result
            = await _graphqlService.GetAdditionalGhosts(_additionalGhosts, _levelHash);

        if (ct.IsCancellationRequested)
            return Result.Ok();

        if (result.IsFailed)
            _logger.LogError("Failed to load additional ghosts: {Result}", result);

        return result;
    }

    public void AddAdditionalGhost(string steamId)
    {
        _additionalGhosts.Add(steamId);
    }

    public bool ContainsAdditionalGhost(string steamId)
    {
        return _additionalGhosts.Contains(steamId);
    }

    public void RemoveAdditionalGhost(string steamId)
    {
        _additionalGhosts.Remove(steamId);
    }
}
