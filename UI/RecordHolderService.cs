using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Microsoft.Extensions.Logging;
using Steamworks;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Messaging;
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
    private readonly MessengerService _messengerService;

    private CancellationTokenSource _cts;
    private IGetWorldRecordHolder_AllWorldRecordGlobals_Nodes _worldRecordHolder;
    private IGetPersonalBest_AllPersonalBestGlobals_Nodes _personalBestHolder;

    private float _timer;

    public RecordHolderService(
        RecordHolderGraphqlService recordHolderGraphqlService,
        ConfigService configService,
        PlayerLoopService playerLoopService,
        ILogger<RecordHolderService> logger,
        MessengerService messengerService)
    {
        _recordHolderGraphqlService = recordHolderGraphqlService;
        _configService = configService;
        _logger = logger;
        _messengerService = messengerService;

        MultiplayerApi.DisconnectedFromGame += OnDisconnectedFromGame;
        RacingApi.Quit += OnQuit;
        RacingApi.PlayerSpawned += OnPlayerSpawned;

        playerLoopService.SubscribeUpdate(OnUpdate);
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
        if (string.IsNullOrEmpty(LevelApi.CurrentHash))
        {
            _logger.LogError("Unable to get level hash");
            _worldRecordHolder = null;
            _personalBestHolder = null;
            return;
        }

        UniTask<Result<IGetWorldRecordHolder_AllWorldRecordGlobals_Nodes>> worldRecordTask =
            _recordHolderGraphqlService.GetWorldRecordHolder(LevelApi.CurrentHash, ct);
        UniTask<Result<IGetPersonalBest_AllPersonalBestGlobals_Nodes>> personalBestTask =
            _recordHolderGraphqlService.GetPersonalBestHolder(LevelApi.CurrentHash, SteamClient.SteamId.Value, ct);

        (Result<IGetWorldRecordHolder_AllWorldRecordGlobals_Nodes> worldRecordResult,
                Result<IGetPersonalBest_AllPersonalBestGlobals_Nodes> personalBestResult) =
            await UniTask.WhenAll(worldRecordTask, personalBestTask);

        if (ct.IsCancellationRequested)
        {
            _worldRecordHolder = null;
            _personalBestHolder = null;
            return;
        }

        if (worldRecordResult.IsFailed)
        {
            _logger.LogError("Failed to get world record holder: {Result}", worldRecordResult);
            _worldRecordHolder = null;
            _personalBestHolder = null;
            return;
        }

        if (personalBestResult.IsFailed)
        {
            _logger.LogError("Failed to get personal best holder: {Result}", personalBestResult);
            _worldRecordHolder = null;
            _personalBestHolder = null;
            return;
        }

        _worldRecordHolder = worldRecordResult.Value;
        _personalBestHolder = personalBestResult.Value;

        Result<int> rankResult =
            await _recordHolderGraphqlService.GetRank(LevelApi.CurrentHash, _personalBestHolder.RecordByIdRecord.Time,
                ct);

        if (ct.IsCancellationRequested)
        {
            _worldRecordHolder = null;
            _personalBestHolder = null;
            return;
        }

        if (rankResult.IsFailed)
        {
            _logger.LogError("Failed to get rank: {Result}", rankResult);
            _worldRecordHolder = null;
            _personalBestHolder = null;
            return;
        }

        _timer = _configService.RecordHolderSwitchTime.Value;
        RecordHolderUi.Create(_worldRecordHolder, _personalBestHolder, rankResult.Value);
    }

    private void CheckKeyDown(ConfigEntry<KeyCode> keyConfig, ConfigEntry<bool> showConfig, string positive,
        string negative)
    {
        if (Input.GetKeyDown(keyConfig.Value))
        {
            showConfig.Value = !showConfig.Value;
            _messengerService.Log(showConfig.Value ? positive : negative);
        }
    }

    private void OnUpdate()
    {
        CheckKeyDown(_configService.ToggleShowRecordHolder, _configService.ShowRecordHolder,
            "Showing Combined Record Holder",
            "Hiding Combined Record Holder");

        CheckKeyDown(_configService.ToggleShowWorldRecordHolder, _configService.ShowWorldRecordHolder,
            "Showing World Record Holder",
            "Hiding World Record Holder");

        CheckKeyDown(_configService.ToggleShowPersonalBestHolder, _configService.ShowPersonalBestHolder,
            "Showing Personal Best Holder",
            "Hiding Personal Best Holder");

        CheckKeyDown(_configService.ToggleShowWorldRecordOnHolder, _configService.ShowWorldRecordOnHolder,
            "Showing World Record On Combined",
            "Hiding World Record On Combined");

        CheckKeyDown(_configService.ToggleShowPersonalBestOnHolder, _configService.ShowPersonalBestOnHolder,
            "Showing Personal Best On Combined",
            "Hiding Personal Best On Combined");

        if (_worldRecordHolder == null || _personalBestHolder == null)
            return;

        _timer -= Time.deltaTime;

        if (_timer <= 0)
        {
            RecordHolderUi.SwitchToNext();
            _timer = _configService.RecordHolderSwitchTime.Value;
        }
    }

    private void OnQuit()
    {
        _worldRecordHolder = null;
        _personalBestHolder = null;
        RecordHolderUi.Disable();
    }

    private void OnDisconnectedFromGame()
    {
        _worldRecordHolder = null;
        _personalBestHolder = null;
        RecordHolderUi.Disable();
    }
}
