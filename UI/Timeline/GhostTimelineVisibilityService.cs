using System;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;

namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GhostTimelineVisibilityService : IEagerService
{
    private readonly ConfigService _configService;
    private readonly GhostTimelineState _timelineState;

    public GhostTimelineVisibilityService(
        ConfigService configService,
        GhostTimelineState timelineState)
    {
        _configService = configService;
        _timelineState = timelineState;

        _timelineState.SetVisible(_configService.ShowTimeline.Value);
        _configService.ShowTimeline.SettingChanged += OnShowTimelineChanged;
    }

    private void OnShowTimelineChanged(object sender, EventArgs e)
    {
        _timelineState.SetVisible(_configService.ShowTimeline.Value);
    }
}
