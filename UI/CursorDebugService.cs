using System;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.UI;

public class CursorDebugService : IEagerService, IDisposable
{
    private readonly ConfigService _configService;
    private readonly MessengerService _messengerService;
    private readonly PlayerLoopService _playerLoopService;
    private readonly PlayerLoopSubscription _updateSubscription;

    public CursorDebugService(
        PlayerLoopService playerLoopService,
        ConfigService configService,
        MessengerService messengerService)
    {
        _playerLoopService = playerLoopService;
        _configService = configService;
        _messengerService = messengerService;
        _updateSubscription = _playerLoopService.SubscribeUpdate(OnUpdate);
    }

    private void OnUpdate()
    {
        if (!Input.GetKeyDown(_configService.ToggleCursorEnabled.Value))
            return;

        var cursorManager = PlayerManager.Instance?.cursorManager;
        if (cursorManager == null)
            return;

        cursorManager.SetEnabled(!cursorManager.IsEnabled);
        _messengerService.Log(cursorManager.IsEnabled ? "Cursor enabled" : "Cursor disabled");
    }

    public void Dispose()
    {
        _playerLoopService.UnsubscribeUpdate(_updateSubscription);
    }
}
