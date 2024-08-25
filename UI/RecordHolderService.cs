using System.Threading;
using Microsoft.Extensions.Logging;
using Steamworks;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Level;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.UI;

public class RecordHolderService : IEagerService
{
    private readonly RecordHolderGraphqlService _recordHolderGraphqlService;
    private readonly ConfigService _configService;
    private readonly ILogger<RecordHolderService> _logger;

    private CancellationTokenSource _cts;
    private string _levelHash;
    private RecordHolders _recordHolders;

    private float _timer;

    public RecordHolderService(
        RecordHolderGraphqlService recordHolderGraphqlService,
        ConfigService configService,
        PlayerLoopService playerLoopService,
        ILogger<RecordHolderService> logger)
    {
        _recordHolderGraphqlService = recordHolderGraphqlService;
        _configService = configService;
        _logger = logger;

        MultiplayerApi.DisconnectedFromGame += OnDisconnectedFromGame;
        RacingApi.Quit += OnQuit;
        RacingApi.LevelLoaded += OnLevelLoaded;
        RacingApi.PlayerSpawned += OnPlayerSpawned;

        playerLoopService.SubscribeUpdate(OnUpdate);
    }

    private void OnLevelLoaded()
    {
        _levelHash = LevelApi.GetLevelHash(LevelApi.CurrentLevel);
    }

    private void OnPlayerSpawned()
    {
        RecordHolderUi.EnsureExists();
        GetRecordHolders();
    }

    private void GetRecordHolders()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        GetRecordHoldersAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid GetRecordHoldersAsync(CancellationToken ct = default)
    {
        Result<RecordHolders> result
            = await _recordHolderGraphqlService.GetRecordHolders(_levelHash, SteamClient.SteamId.Value);

        if (result.IsFailed)
        {
            _logger.LogError("Failed to get record holders: {Result}", result);
            _recordHolders = null;
            return;
        }

        _recordHolders = result.Value;
        _timer = 10;
        RecordHolderUi.Create(_recordHolders);
    }

    private void OnUpdate()
    {
        if (_recordHolders == null)
            return;

        _timer -= Time.deltaTime;

        if (_timer <= 0)
        {
            RecordHolderUi.SwitchToNext();
            _timer = 10;
        }
    }

    private void OnQuit()
    {
        _recordHolders = null;
        RecordHolderUi.Disable();
    }

    private void OnDisconnectedFromGame()
    {
        _recordHolders = null;
        RecordHolderUi.Disable();
    }
}
