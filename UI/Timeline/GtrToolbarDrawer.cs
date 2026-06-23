using System;
using Imui.Controls;
using Imui.Core;
using Imui.Rendering;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.UI.Spectate;
using UnityEngine;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GtrToolbarDrawer : IZeepToolbarDrawer
{
    private readonly PhotoModeTimelineService _photoModeTimelineService;
    private readonly GhostTimelineState _timelineState;
    private readonly GhostSpectateState _spectateState;

    public GtrToolbarDrawer(
        PhotoModeTimelineService photoModeTimelineService,
        GhostTimelineState timelineState,
        GhostSpectateState spectateState)
    {
        _photoModeTimelineService = photoModeTimelineService;
        _timelineState = timelineState;
        _spectateState = spectateState;
    }

    public string MenuTitle => "GTR";

    public void DrawMenuItems(ImGui gui)
    {
        if (_photoModeTimelineService.IsPhotoModeGhostsAvailable)
        {
            if (gui.Menu("Playback".AsSpan(), _timelineState.IsVisible))
                _timelineState.ToggleVisible();

            if (gui.Menu("Spectate".AsSpan(), _spectateState.IsVisible))
                _spectateState.ToggleVisible();
            return;
        }

        gui.Text("Playback".AsSpan(),
            new Color32(128, 128, 128, 255));
        gui.Text("Spectate".AsSpan(),
            new Color32(128, 128, 128, 255));
    }
}
