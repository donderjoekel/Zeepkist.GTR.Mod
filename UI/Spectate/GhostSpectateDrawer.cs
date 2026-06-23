using System;
using Imui.Controls;
using Imui.Core;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using UnityEngine;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Spectate;

public class GhostSpectateDrawer : IZeepGUIDrawer
{
    private const string WindowTitle = "GTR Spectate";
    private const float DefaultWindowWidth = 420f;
    private const float ListHeight = 240f;

    private readonly GhostPlayer _ghostPlayer;
    private readonly GhostSpectateService _spectateService;
    private readonly GhostSpectateState _spectateState;
    private readonly PhotoModeTimelineService _photoModeTimelineService;

    private bool _mouseOverWindow;
    private int _selectedListIndex;

    public GhostSpectateDrawer(
        GhostPlayer ghostPlayer,
        GhostSpectateService spectateService,
        GhostSpectateState spectateState,
        PhotoModeTimelineService photoModeTimelineService)
    {
        _ghostPlayer = ghostPlayer;
        _spectateService = spectateService;
        _spectateState = spectateState;
        _photoModeTimelineService = photoModeTimelineService;
    }

    public void OnZeepGUI(ImGui gui)
    {
        if (!_spectateState.IsVisible || !_photoModeTimelineService.IsPhotoModeGhostsAvailable)
            return;

        SyncSelectedListIndex();

        var open = true;
        ImSize windowSize = GetWindowSize(gui);
        if (!gui.BeginWindow(WindowTitle, ref open, ref _mouseOverWindow, windowSize))
            return;

        try
        {
            DrawContent(gui);
        }
        finally
        {
            gui.EndWindow();
        }

        if (!open)
            _spectateState.SetVisible(false);
    }

    private void DrawContent(ImGui gui)
    {
        var loadedGhosts = _ghostPlayer.GetLoadedGhosts();
        var selectedName = _spectateService.GetSelectedDisplayName();
        if (selectedName != null)
        {
            gui.Text($"Spectating: {selectedName}".AsSpan());
        }
        else
        {
            gui.Text("Spectating: None".AsSpan());
        }

        gui.AddSpacing(gui.Style.Layout.Spacing);
        gui.Text("Ghosts".AsSpan());

        ImSize listSize = new Vector2(gui.GetLayoutWidth(), ListHeight);
        using (gui.Scrollable())
        using (gui.List(listSize))
        {
            if (gui.ListItem(ref _selectedListIndex, 0, "None".AsSpan()))
                _spectateService.ClearSelection();

            for (var i = 0; i < loadedGhosts.Count; i++)
            {
                LoadedGhostEntry ghost = loadedGhosts[i];
                int listIndex = i + 1;
                if (gui.ListItem(ref _selectedListIndex, listIndex, ghost.DisplayName.AsSpan()))
                    _spectateService.SelectGhost(ghost.RecordId);
            }
        }

        gui.AddSpacing(gui.Style.Layout.Spacing);
        gui.Text("Camera Mode".AsSpan());
        DrawCameraModeRadios(gui);
    }

    private void DrawCameraModeRadios(ImGui gui)
    {
        var mode = _spectateService.CameraMode;

        if (gui.Radio(mode == GhostSpectateCameraMode.FirstPerson, "First Person".AsSpan()))
            _spectateService.SetCameraMode(GhostSpectateCameraMode.FirstPerson);

        if (gui.Radio(mode == GhostSpectateCameraMode.ThirdPersonStrict, "Third Person (Strict)".AsSpan()))
            _spectateService.SetCameraMode(GhostSpectateCameraMode.ThirdPersonStrict);

        if (gui.Radio(mode == GhostSpectateCameraMode.ThirdPersonSmooth, "Third Person (Smooth)".AsSpan()))
            _spectateService.SetCameraMode(GhostSpectateCameraMode.ThirdPersonSmooth);
    }

    private void SyncSelectedListIndex()
    {
        if (!_spectateService.SelectedRecordId.HasValue)
        {
            _selectedListIndex = 0;
            return;
        }

        var loadedGhosts = _ghostPlayer.GetLoadedGhosts();
        for (var i = 0; i < loadedGhosts.Count; i++)
        {
            if (loadedGhosts[i].RecordId != _spectateService.SelectedRecordId.Value)
                continue;

            _selectedListIndex = i + 1;
            return;
        }

        _selectedListIndex = 0;
    }

    private static ImSize GetWindowSize(ImGui gui)
    {
        const int contentRows = 8;
        var contentHeight = gui.GetRowsHeightWithSpacing(contentRows);
        var windowPadding = gui.Style.Window.ContentPadding.Vertical;
        var height = contentHeight + ImWindow.GetTitleBarHeight(gui) + windowPadding + ListHeight;
        return new Vector2(DefaultWindowWidth, height);
    }
}
