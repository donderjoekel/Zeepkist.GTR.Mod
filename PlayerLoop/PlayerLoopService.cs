using System;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.PlayerLoop;

public partial class PlayerLoopService : IEagerService
{
    private readonly PlayerLoopBehaviour _behaviour;
    private readonly ILogger<PlayerLoopService> _logger;

    public PlayerLoopService(ILogger<PlayerLoopService> logger)
    {
        _logger = logger;
        GameObject gameObject = new GameObject("PlayerLoopService");
        Object.DontDestroyOnLoad(gameObject);
        _behaviour = gameObject.AddComponent<PlayerLoopBehaviour>();
        _behaviour.SetLogger(_logger);
    }

    public PlayerLoopSubscription SubscribeUpdate(Action action)
    {
        return _behaviour.SubscribeUpdate(action);
    }

    public void UnsubscribeUpdate(PlayerLoopSubscription subscription)
    {
        _behaviour.UnsubscribeUpdate(subscription);
    }

    public PlayerLoopSubscription SubscribeFixedUpdate(Action action)
    {
        return _behaviour.SubscribeFixedUpdate(action);
    }

    public void UnsubscribeFixedUpdate(PlayerLoopSubscription subscription)
    {
        _behaviour.UnsubscribeFixedUpdate(subscription);
    }
}
