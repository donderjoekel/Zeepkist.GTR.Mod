using System;
using Imui.Controls;
using Imui.Core;
using Imui.Rendering;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using UnityEngine;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GtrToolbarDrawer : IZeepToolbarDrawer
{
    private readonly PhotoModeTimelineService _photoModeTimelineService;
    private readonly GhostTimelineState _timelineState;

    public GtrToolbarDrawer(
        PhotoModeTimelineService photoModeTimelineService,
        GhostTimelineState timelineState)
    {
        _photoModeTimelineService = photoModeTimelineService;
        _timelineState = timelineState;
    }

    public string MenuTitle => "GTR";

    public void DrawMenuItems(ImGui gui)
    {
        if (_photoModeTimelineService.IsTimelineAvailable)
        {
            if (gui.Menu("Playback".AsSpan(), _timelineState.IsVisible))
                _timelineState.ToggleVisible();
            return;
        }

        gui.Text("Playback".AsSpan(),
            new Color32(128, 128, 128, 255));
    }
}
