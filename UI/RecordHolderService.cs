using System;
using BepInEx.Configuration;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.UI;

public class RecordHolderService : IEagerService, IDisposable
{
    private readonly CurrentLevelRecordService _currentLevelRecordService;
    private readonly ConfigService _configService;
    private readonly MessengerService _messengerService;
    private readonly PlayerLoopService _playerLoopService;
    private readonly PlayerLoopSubscription _updateSubscription;

    private readonly RecordHolderPresentationState _presentation = new();

    public RecordHolderService(
        CurrentLevelRecordService currentLevelRecordService,
        ConfigService configService,
        PlayerLoopService playerLoopService,
        MessengerService messengerService)
    {
        _currentLevelRecordService = currentLevelRecordService;
        _configService = configService;
        _messengerService = messengerService;
        _playerLoopService = playerLoopService;

        MultiplayerApi.DisconnectedFromGame += OnDisconnectedFromGame;
        RacingApi.Quit += OnQuit;
        RacingApi.PlayerSpawned += OnPlayerSpawned;
        RacingApi.LevelLoaded += OnLevelLoaded;
        _currentLevelRecordService.SnapshotChanged += OnSnapshotChanged;
        RecordHolderUi.CombinedViewChanged += ResetTimer;
        _updateSubscription = _playerLoopService.SubscribeUpdate(OnUpdate);
    }

    internal void InitializeCurrentLevel() => OnLevelLoaded();

    private void OnLevelLoaded()
    {
        _presentation.Reset();
        ResetTimer();
        RecordHolderUi.EnsureExists();
        RecordHolderUi.Create(null, null);
    }

    private void OnPlayerSpawned()
    {
        RecordHolderUi.EnsureExists();
        OnSnapshotChanged(_currentLevelRecordService.Snapshot);
    }

    private void OnSnapshotChanged(CurrentLevelRecordSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        RecordHolderUpdate update = _presentation.Observe(snapshot.LevelKey, snapshot.PersonalBest);
        RecordHolderUi.Create(snapshot.WorldRecord, snapshot.PersonalBest,
            update.PersonalBestChanged, update.MedalUpgrade);
        if (update.PersonalBestChanged) ResetTimer();
    }

    private void ResetTimer() => _presentation.ResetTimer(_configService.RecordHolderSwitchTime.Value);

    private void CheckKeyDown(
        ConfigEntry<KeyCode> keyConfig,
        ConfigEntry<bool> showConfig,
        string positive,
        string negative)
    {
        if (!Input.GetKeyDown(keyConfig.Value))
            return;

        showConfig.Value = !showConfig.Value;
        _messengerService.Log(showConfig.Value ? positive : negative);
    }

    private void OnUpdate()
    {
        CheckKeyDown(_configService.ToggleShowRecordHolder, _configService.ShowRecordHolder,
            "Showing Combined Record Holder", "Hiding Combined Record Holder");
        CheckKeyDown(_configService.ToggleShowWorldRecordHolder, _configService.ShowWorldRecordHolder,
            "Showing World Record Holder", "Hiding World Record Holder");
        CheckKeyDown(_configService.ToggleShowPersonalBestHolder, _configService.ShowPersonalBestHolder,
            "Showing Personal Best Holder", "Hiding Personal Best Holder");
        CheckKeyDown(_configService.ToggleShowWorldRecordOnHolder, _configService.ShowWorldRecordOnHolder,
            "Showing World Record On Combined", "Hiding World Record On Combined");
        CheckKeyDown(_configService.ToggleShowPersonalBestOnHolder, _configService.ShowPersonalBestOnHolder,
            "Showing Personal Best On Combined", "Hiding Personal Best On Combined");

        if (!_presentation.Tick(Time.deltaTime))
            return;

        RecordHolderUi.SwitchToNext();
        ResetTimer();
    }

    private void OnQuit()
    {
        _presentation.Reset();
        RecordHolderUi.Disable();
    }

    private void OnDisconnectedFromGame() => OnQuit();

    public void Dispose()
    {
        MultiplayerApi.DisconnectedFromGame -= OnDisconnectedFromGame;
        RacingApi.Quit -= OnQuit;
        RacingApi.PlayerSpawned -= OnPlayerSpawned;
        RacingApi.LevelLoaded -= OnLevelLoaded;
        _currentLevelRecordService.SnapshotChanged -= OnSnapshotChanged;
        RecordHolderUi.CombinedViewChanged -= ResetTimer;
        _playerLoopService.UnsubscribeUpdate(_updateSubscription);
    }
}
