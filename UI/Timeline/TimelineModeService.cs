using System;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.PlayerLoop;

namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class TimelineModeService : IEagerService, IDisposable
{
    private readonly GhostTimelineState _timelineState;
    private readonly PhotoModeTimelineService _photoModeTimelineService;
    private readonly PlayerLoopService _playerLoopService;
    private readonly PlayerLoopSubscription _updateSubscription;

    private bool _wasActive;

    public TimelineModeService(
        GhostTimelineState timelineState,
        PhotoModeTimelineService photoModeTimelineService,
        PlayerLoopService playerLoopService)
    {
        _timelineState = timelineState;
        _photoModeTimelineService = photoModeTimelineService;
        _playerLoopService = playerLoopService;
        _updateSubscription = _playerLoopService.SubscribeUpdate(Update);
    }

    public bool IsActive =>
        _timelineState.ShouldShow && _photoModeTimelineService.IsTimelineAvailable;

    private void Update()
    {
        var isActive = IsActive;

        if (isActive && !_wasActive)
            EnterTimelineMode();
        else if (!isActive && _wasActive)
            ExitTimelineMode();

        if (isActive)
            EnsureCursorVisible();

        _wasActive = isActive;
    }

    private static void EnterTimelineMode()
    {
        var cursorManager = PlayerManager.Instance?.cursorManager;
        if (cursorManager == null)
            return;

        cursorManager.SetEnabled(false);
        cursorManager.SetCursorEnabled(true);
    }

    private static void EnsureCursorVisible()
    {
        PlayerManager.Instance?.cursorManager?.SetCursorEnabled(true);
    }

    private static void ExitTimelineMode()
    {
        var cursorManager = PlayerManager.Instance?.cursorManager;
        if (cursorManager == null)
            return;

        cursorManager.SetEnabled(true);
        cursorManager.SetCursorEnabled(false);
    }

    public void Dispose()
    {
        if (_wasActive)
            ExitTimelineMode();

        _playerLoopService.UnsubscribeUpdate(_updateSubscription);
    }
}
