using System;
using Imui.Controls;
using Imui.Core;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.UI;
using UnityEngine;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Spectate;

public class GhostSpectateDrawer : IZeepGUIDrawer
{
    private const string WindowTitle = "GTR Spectate";
    private const float DefaultWindowWidth = 480f;
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
        ImRect windowRect = ImWindowPlacement.GetRect(
            gui,
            WindowTitle.AsSpan(),
            windowSize.Width,
            windowSize.Height,
            ImWindowAnchor.MiddleLeft);
        if (!gui.BeginWindow(WindowTitle, ref open, ref _mouseOverWindow, windowRect))
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
        using (gui.Vertical())
        {
            DrawGhostSelection(gui);
            DrawCameraModeSection(gui);
        }
    }

    private void DrawGhostSelection(ImGui gui)
    {
        var selectedName = _spectateService.GetSelectedDisplayName();
        if (selectedName != null)
            gui.Text($"Spectating: {selectedName}".AsSpan());
        else
            gui.Text("Spectating: None".AsSpan());

        gui.AddSpacing(gui.Style.Layout.Spacing);
        gui.Text("Ghosts".AsSpan());

        var loadedGhosts = _ghostPlayer.GetLoadedGhosts();
        ImSize listSize = new Vector2(
            gui.GetLayoutWidth(),
            ImList.GetEnclosingHeight(gui, ListHeight));

        using (gui.List(listSize))
        {
            if (gui.ListItem(ref _selectedListIndex, 0, "None".AsSpan()))
                _spectateService.ClearSelection();

            for (var i = 0; i < loadedGhosts.Count; i++)
            {
                LoadedGhostEntry ghost = loadedGhosts[i];
                int listIndex = i + 1;
                if (gui.ListItem(ref _selectedListIndex, listIndex, ghost.GetListLabel().AsSpan()))
                    _spectateService.SelectGhost(ghost.RecordId);
            }
        }
    }

    private void DrawCameraModeSection(ImGui gui)
    {
        gui.AddSpacing(gui.Style.Layout.Spacing);
        gui.Text("Camera Mode".AsSpan());
        DrawCameraModeRadios(gui);
    }

    private void DrawCameraModeRadios(ImGui gui)
    {
        var mode = _spectateService.CameraMode;

        var firstPerson = mode == GhostSpectateCameraMode.FirstPerson;
        if (gui.Radio(ref firstPerson, "First person".AsSpan()))
            _spectateService.SetCameraMode(GhostSpectateCameraMode.FirstPerson);

        var thirdPerson = mode == GhostSpectateCameraMode.ThirdPersonStrict;
        if (gui.Radio(ref thirdPerson, "Third person".AsSpan()))
            _spectateService.SetCameraMode(GhostSpectateCameraMode.ThirdPersonStrict);

        var smoothThirdPerson = mode == GhostSpectateCameraMode.ThirdPersonSmooth;
        if (gui.Radio(ref smoothThirdPerson, "Smooth".AsSpan()))
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
        const int labelRows = 4;
        var labelHeight = gui.GetRowsHeightWithSpacing(labelRows);
        var listHeight = ImList.GetEnclosingHeight(gui, ListHeight);
        var windowPadding = gui.Style.Window.ContentPadding.Vertical;
        var height = labelHeight + listHeight + ImWindow.GetTitleBarHeight(gui) + windowPadding;
        return new Vector2(DefaultWindowWidth, height);
    }
}
