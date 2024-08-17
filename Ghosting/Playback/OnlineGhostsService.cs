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

public class OnlineGhostsService : IEagerService
{
    private readonly ILogger<OnlineGhostsService> _logger;
    private readonly OnlineGhostGraphqlService _onlineGhostGraphqlService;
    private readonly GhostRepository _ghostRepository;
    private readonly GhostPlayer _ghostPlayer;
    private readonly ConfigService _configService;
    private readonly PlayerLoopService _playerLoopService;
    private readonly MessengerService _messengerService;

    private CancellationTokenSource _cts;
    private string _levelHash;

    public OnlineGhostsService(
        ILogger<OnlineGhostsService> logger,
        GhostRepository ghostRepository,
        GhostPlayer ghostPlayer,
        ConfigService configService,
        PlayerLoopService playerLoopService,
        MessengerService messengerService,
        OnlineGhostGraphqlService onlineGhostGraphqlService)
    {
        _logger = logger;
        _ghostRepository = ghostRepository;
        _ghostPlayer = ghostPlayer;
        _configService = configService;
        _playerLoopService = playerLoopService;
        _messengerService = messengerService;
        _onlineGhostGraphqlService = onlineGhostGraphqlService;
        _playerLoopService.SubscribeUpdate(OnUpdate);

        RacingApi.LevelLoaded += OnLevelLoaded;
        RacingApi.PlayerSpawned += OnPlayerSpawned;
        RacingApi.RoundEnded += OnRoundEnded;
        MultiplayerApi.DisconnectedFromGame += OnDisconnectedFromGame;
    }

    private void OnUpdate()
    {
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
        _ghostPlayer.ClearGhosts();
    }

    private void OnLevelLoaded()
    {
        _levelHash = LevelApi.GetLevelHash(LevelApi.CurrentLevel);
    }

    private void OnPlayerSpawned()
    {
        if (_configService.EnableGhosts.Value)
        {
            LoadPersonalBest();
        }
    }

    private void OnRoundEnded()
    {
        _ghostPlayer.ClearGhosts();
    }

    private void LoadPersonalBest()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        LoadPersonalBestAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid LoadPersonalBestAsync(CancellationToken ct)
    {
        _logger.LogInformation("Loading personal best...");

        Result<List<OnlineGhostGraphqlService.PersonalBest>> result
            = await _onlineGhostGraphqlService.GetPersonalBests(_levelHash);

        if (ct.IsCancellationRequested)
            return;

        if (result.IsFailed)
        {
            _logger.LogError("Failed to load personal best: {Result}", result.ToString());
            return;
        }

        List<OnlineGhostGraphqlService.PersonalBest> personalBests = result.Value;

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
}
