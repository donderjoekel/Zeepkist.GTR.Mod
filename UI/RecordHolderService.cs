using System.Threading;
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
    private string _levelHash;
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
        _timer = _configService.RecordHolderSwitchTime.Value;
        RecordHolderUi.Create(_recordHolders);
    }

    private void OnUpdate()
    {
        if (Input.GetKeyDown(_configService.ToggleShowRecordHolder.Value))
        {
            _configService.ShowRecordHolder.Value = !_configService.ShowRecordHolder.Value;

            if (_configService.ShowRecordHolder.Value)
            {
                _messengerService.Log("Showing Record Holder");
            }
            else
            {
                _messengerService.Log("Hiding Record Holder");
            }
        }

        if (Input.GetKeyDown(_configService.ToggleShowWorldRecordOnHolder.Value))
        {
            _configService.ShowWorldRecordOnHolder.Value = !_configService.ShowWorldRecordOnHolder.Value;

            if (_configService.ShowWorldRecordOnHolder.Value)
            {
                _messengerService.Log("Showing World Record Holder");
            }
            else
            {
                _messengerService.Log("Hiding World Record Holder");
            }
        }

        if (Input.GetKeyDown(_configService.ToggleShowPersonalBestOnHolder.Value))
        {
            _configService.ShowPersonalBestOnHolder.Value = !_configService.ShowPersonalBestOnHolder.Value;

            if (_configService.ShowPersonalBestOnHolder.Value)
            {
                _messengerService.Log("Showing Personal Best Holder");
            }
            else
            {
                _messengerService.Log("Hiding Personal Best Holder");
            }
        }

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
