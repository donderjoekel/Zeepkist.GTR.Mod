using System;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.PlayerLoop;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GhostTimelineUiService : IEagerService, IDisposable
{
    private readonly GhostTimelineState _timelineState;
    private readonly PhotoModeTimelineService _photoModeTimelineService;
    private readonly PlayerLoopService _playerLoopService;
    private readonly PlayerLoopSubscription _updateSubscription;

    public GhostTimelineUiService(
        GhostTimelineDrawer timelineDrawer,
        GtrToolbarDrawer toolbarDrawer,
        GhostTimelineState timelineState,
        PhotoModeTimelineService photoModeTimelineService,
        PlayerLoopService playerLoopService)
    {
        _timelineState = timelineState;
        _photoModeTimelineService = photoModeTimelineService;
        _playerLoopService = playerLoopService;
        _updateSubscription = _playerLoopService.SubscribeUpdate(EnsureCursorEnabledWhenTimelineVisible);

        UIApi.AddZeepGUIDrawer(timelineDrawer);
        UIApi.AddToolbarDrawer(toolbarDrawer);
    }

    private void EnsureCursorEnabledWhenTimelineVisible()
    {
        if (!_timelineState.IsVisible || !_photoModeTimelineService.IsTimelineAvailable)
            return;

        PlayerManager.Instance?.cursorManager?.SetCursorEnabled(true);
    }

    public void Dispose()
    {
        _playerLoopService.UnsubscribeUpdate(_updateSubscription);
    }
}
