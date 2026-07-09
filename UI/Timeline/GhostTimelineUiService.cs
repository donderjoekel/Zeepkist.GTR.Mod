using TNRD.Zeepkist.GTR.Core;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GhostTimelineUiService : IEagerService
{
    public GhostTimelineUiService(
        GhostTimelineDrawer timelineDrawer,
        GtrToolbarDrawer toolbarDrawer,
        GhostTimelineState timelineState)
    {
        _ = timelineState;
        UIApi.AddZeepGUIDrawer(timelineDrawer);
        UIApi.AddToolbarDrawer(toolbarDrawer);
    }
}
