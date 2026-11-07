using System;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public sealed partial class GhostEnablementService : IDisposable
{
    private readonly Func<bool> _isEnabled;
    private readonly Action<EventHandler> _unsubscribe;
    private readonly Action<Action> _onMainThread;
    private readonly Action _cancelLoads;
    private readonly Action _disable;
    private readonly Action _reload;
    private bool _disposed;

    internal GhostEnablementService(
        Func<bool> isEnabled,
        Action<EventHandler> subscribe,
        Action<EventHandler> unsubscribe,
        Action<Action> onMainThread,
        Action cancelLoads,
        Action disable,
        Action reload)
    {
        _isEnabled = isEnabled;
        _unsubscribe = unsubscribe;
        _onMainThread = onMainThread;
        _cancelLoads = cancelLoads;
        _disable = disable;
        _reload = reload;
        subscribe(OnSettingChanged);
    }

    private void OnSettingChanged(object sender, EventArgs args) => _onMainThread(() =>
    {
        if (_disposed) return;
        _cancelLoads();
        if (_isEnabled()) _reload();
        else _disable();
    });

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _unsubscribe(OnSettingChanged);
    }
}
