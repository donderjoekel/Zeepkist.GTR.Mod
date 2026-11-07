using System;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Patching.Patches;
using TNRD.Zeepkist.GTR.PlayerLoop;
using TNRD.Zeepkist.GTR.UI;
using UnityEngine;
using ZeepSDK.Controls;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.Dialogs;

internal sealed class UnsupportedVersionService : IEagerService, IDisposable
{
    private readonly GtrVersionStatus _version;
    private readonly ConfigService _config;
    private readonly PlayerLoopService _playerLoop;
    private readonly PlayerLoopSubscription _subscription;
    private UnsupportedVersionDialog _dialog;
    private bool _menuReady;
    private bool _shown;
    private bool _recordsVisible;
    private bool _recordsCreated;
    private float _timer;

    public UnsupportedVersionService(GtrVersionStatus version, ConfigService config, PlayerLoopService playerLoop)
    {
        _version = version;
        _config = config;
        _playerLoop = playerLoop;
        MainMenuUi_Awake.Postfixed += OnMenuReady;
        RacingApi.LevelLoaded += ShowRecords;
        RacingApi.PlayerSpawned += ShowRecords;
        RacingApi.Quit += HideRecords;
        MultiplayerApi.DisconnectedFromGame += HideRecords;
        _subscription = playerLoop.SubscribeUpdate(OnUpdate);
        if (UnityEngine.Object.FindObjectOfType<MainMenuUI>() == null &&
            PlayerManager.Instance?.currentMaster != null)
            ShowRecords();
    }

    private void OnMenuReady() => _menuReady = true;

    private void OnUpdate()
    {
        if (_menuReady && !_shown && ControlsApi.MenuInputOverride.Value)
        {
            _shown = true;
            _dialog = new UnsupportedVersionDialog(_version, RemoveDialog);
            UIApi.AddZeepGUIDrawer(_dialog);
        }
        if (!_recordsVisible)
            return;
        if (!_recordsCreated)
        {
            ShowRecords();
            if (!_recordsCreated)
                return;
        }
        _timer -= Time.deltaTime;
        if (_timer <= 0)
        {
            RecordHolderUi.SwitchToNext();
            _timer = _config.RecordHolderSwitchTime.Value;
        }
    }

    private void ShowRecords()
    {
        _recordsVisible = true;
        _recordsCreated = RecordHolderUi.HasTemplate;
        if (!_recordsCreated)
            return;
        RecordHolderUi.Create(null, null);
        _timer = _config.RecordHolderSwitchTime.Value;
    }

    private void HideRecords()
    {
        _recordsVisible = false;
        _recordsCreated = false;
        RecordHolderUi.DestroyInstances();
    }

    private void RemoveDialog()
    {
        if (_dialog == null)
            return;
        UIApi.RemoveZeepGUIDrawer(_dialog);
        _dialog.Dispose();
        _dialog = null;
    }

    public void Dispose()
    {
        MainMenuUi_Awake.Postfixed -= OnMenuReady;
        RacingApi.LevelLoaded -= ShowRecords;
        RacingApi.PlayerSpawned -= ShowRecords;
        RacingApi.Quit -= HideRecords;
        MultiplayerApi.DisconnectedFromGame -= HideRecords;
        _playerLoop.UnsubscribeUpdate(_subscription);
        RemoveDialog();
        HideRecords();
    }
}
