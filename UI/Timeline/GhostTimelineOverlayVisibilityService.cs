using System;
using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Patching.Patches;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GhostTimelineOverlayVisibilityService : IEagerService, IDisposable
{
    private readonly GhostTimelineState _timelineState;
    private readonly HashSet<BaseUI> _openOverlays = new();

    public GhostTimelineOverlayVisibilityService(GhostTimelineState timelineState)
    {
        _timelineState = timelineState;

        BaseUI_OverlayVisibility.OverlayOpened += OnOverlayOpened;
        BaseUI_OverlayVisibility.OverlayClosed += OnOverlayClosed;
        RacingApi.PlayerSpawned += ResetOverlayState;
        RacingApi.Quit += ResetOverlayState;
    }

    private void OnOverlayOpened(BaseUI ui)
    {
        if (_openOverlays.Add(ui))
            UpdateHiddenState();
    }

    private void OnOverlayClosed(BaseUI ui)
    {
        if (_openOverlays.Remove(ui))
            UpdateHiddenState();
    }

    private void UpdateHiddenState()
    {
        _timelineState.SetHiddenByOverlay(_openOverlays.Count > 0);
    }

    private void ResetOverlayState()
    {
        _openOverlays.Clear();
        _timelineState.SetHiddenByOverlay(false);
    }

    public void Dispose()
    {
        BaseUI_OverlayVisibility.OverlayOpened -= OnOverlayOpened;
        BaseUI_OverlayVisibility.OverlayClosed -= OnOverlayClosed;
        RacingApi.PlayerSpawned -= ResetOverlayState;
        RacingApi.Quit -= ResetOverlayState;
    }
}
