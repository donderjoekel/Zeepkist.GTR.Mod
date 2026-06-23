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
    private readonly SpectatorModeService _spectatorModeService;
    private readonly GhostTimelineState _timelineState;

    public GtrToolbarDrawer(
        SpectatorModeService spectatorModeService,
        GhostTimelineState timelineState)
    {
        _spectatorModeService = spectatorModeService;
        _timelineState = timelineState;
    }

    public string MenuTitle => "GTR";

    public void DrawMenuItems(ImGui gui)
    {
        if (_spectatorModeService.IsTimelineAvailable)
        {
            if (gui.Menu("Timeline".AsSpan(), _timelineState.IsVisible))
                _timelineState.ToggleVisible();
            return;
        }

        gui.Text("Timeline".AsSpan(),
            new Color32(128, 128, 128, 255));
    }
}
