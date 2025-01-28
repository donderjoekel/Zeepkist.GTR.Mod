using System.Threading;
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
    private RecordHolders _recordHolders;

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
            _recordHolders = null;
            return;
        }
        
        Result<RecordHolders> result
            = await _recordHolderGraphqlService.GetRecordHolders(LevelApi.CurrentHash, SteamClient.SteamId.Value);

        if (result.IsFailed)
        {
            _logger.LogError("Failed to get record holders: {Result}", result);
            _recordHolders = null;
            return;
        }

        _recordHolders = result.Value;
        _timer = _configService.RecordHolderSwitchTime.Value;
        RecordHolderUi.Create(_recordHolders);
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

        if (_recordHolders == null)
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
        _recordHolders = null;
        RecordHolderUi.Disable();
    }

    private void OnDisconnectedFromGame()
    {
        _recordHolders = null;
        RecordHolderUi.Disable();
    }
}
