using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Patching.Patches;

namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GhostTimelinePauseMenuService : IEagerService
{
    private readonly GhostTimelineState _timelineState;

    public GhostTimelinePauseMenuService(GhostTimelineState timelineState)
    {
        _timelineState = timelineState;

        PauseMenuUI_OnOpen.Opened += OnPauseMenuOpened;
        PauseMenuUI_Close.Closed += OnPauseMenuClosed;
    }

    private void OnPauseMenuOpened()
    {
        _timelineState.SetHiddenByPauseMenu(true);
    }

    private void OnPauseMenuClosed(bool announce)
    {
        if (announce)
            _timelineState.SetHiddenByPauseMenu(false);
    }
}
