using System;
using Imui.Controls;
using Imui.Core;
using Imui.Rendering;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.UI;
using UnityEngine;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GhostTimelineDrawer : IZeepGUIDrawer
{
    private const string WindowTitle = "GTR Playback";
    private const float DefaultWindowWidth = 960f;
    private readonly PhotoModeTimelineService _photoModeTimelineService;
    private readonly GhostPlaybackService _playbackService;
    private readonly GhostTimelineState _timelineState;
    private readonly PlaybackUiInputState _playbackUiInputState;

    private bool _mouseOverWindow;
    private float _scrubTime;
    private float _speed = 1f;
    private bool _isScrubbing;

    public GhostTimelineDrawer(
        PhotoModeTimelineService photoModeTimelineService,
        GhostPlaybackService playbackService,
        GhostTimelineState timelineState,
        PlaybackUiInputState playbackUiInputState)
    {
        _photoModeTimelineService = photoModeTimelineService;
        _playbackService = playbackService;
        _timelineState = timelineState;
        _playbackUiInputState = playbackUiInputState;
    }

    public void OnZeepGUI(ImGui gui)
    {
        if (!_timelineState.IsVisible || !_photoModeTimelineService.IsTimelineAvailable)
            return;

        var duration = _playbackService.Duration;
        var open = true;
        ImSize windowSize = GetTimelineWindowSize(gui, duration > 0f);
        ImRect windowRect = ImWindowPlacement.GetRect(
            gui,
            WindowTitle.AsSpan(),
            windowSize.Width,
            windowSize.Height,
            ImWindowAnchor.BottomRight);
        if (!gui.BeginWindow(WindowTitle, ref open, ref _mouseOverWindow, windowRect))
            return;

        try
        {
            DrawTimeline(gui, duration);
        }
        finally
        {
            gui.EndWindow();
        }

        if (_mouseOverWindow)
            _playbackUiInputState.NotifyPointerOverGtrWindow();

        if (!open)
            _timelineState.SetVisible(false);
    }

    private static ImSize GetTimelineWindowSize(ImGui gui, bool hasPlaybackData)
    {
        const int playbackContentRows = 5;
        var contentRows = hasPlaybackData ? playbackContentRows : 1;
        var contentHeight = gui.GetRowsHeightWithSpacing(contentRows);
        var windowPadding = gui.Style.Window.ContentPadding.Vertical;
        var height = contentHeight + ImWindow.GetTitleBarHeight(gui) + windowPadding;

        var width = DefaultWindowWidth;
        var windowId = gui.GetControlId(WindowTitle.AsSpan());
        if (gui.WindowManager.TryFindWindow(windowId) >= 0)
            width = gui.WindowManager.GetWindowState(windowId).Rect.W;

        return new Vector2(width, height);
    }

    private void DrawTimeline(ImGui gui, float duration)
    {
        if (duration <= 0f)
        {
            gui.Text("No ghost playback data available".AsSpan());
            return;
        }

        if (!_isScrubbing)
            _scrubTime = _playbackService.CurrentTime;

        using (gui.Vertical())
        {
            DrawTimeScrubber(gui, _scrubTime, duration);
            DrawTransportControls(gui);
            DrawSpeedControl(gui);
        }
    }

    private void DrawTimeScrubber(ImGui gui, float currentTime, float duration)
    {
        var timeDisplay = $"{FormatTime(currentTime)} / {FormatTime(duration)}";
        gui.SliderHeader("Time".AsSpan(), currentTime, timeDisplay.AsSpan());

        ImSize size = new Vector2(gui.GetLayoutWidth(), gui.GetRowHeight());
        var scrubTime = _scrubTime;
        var scrubberChanged = gui.Slider(ref scrubTime, 0f, duration, size, 0.01f);
        _isScrubbing = scrubberChanged || gui.IsControlActive(gui.LastControl);

        if (scrubberChanged)
        {
            _scrubTime = scrubTime;
            _playbackService.Seek(scrubTime);
        }
    }

    private void DrawTransportControls(ImGui gui)
    {
        using (gui.Horizontal())
        {
            var rowHeight = gui.GetRowHeight();
            const float buttonWidth = 72f;
            const int buttonCount = 4;
            var playbackButtonsWidth = buttonWidth * buttonCount;
            var availableWidth = gui.GetLayoutWidth();
            var sideSpacing = Mathf.Max(0f, (availableWidth - playbackButtonsWidth) * 0.5f);

            gui.AddSpacing(sideSpacing);
            DrawPlaybackButtons(gui, rowHeight, buttonWidth);
            gui.AddSpacing(sideSpacing);
        }
    }

    private void DrawPlaybackButtons(ImGui gui, float rowHeight, float buttonWidth)
    {
        ImSize buttonSize = new Vector2(buttonWidth, rowHeight);

        if (gui.Button("-5s".AsSpan(), buttonSize))
            _playbackService.SkipBackward();

        var playPauseLabel = _playbackService.IsPlaying ? "Pause" : "Play";
        if (gui.Button(playPauseLabel.AsSpan(), buttonSize))
            _playbackService.TogglePlayPause();

        if (gui.Button("Stop".AsSpan(), buttonSize))
            _playbackService.Stop();

        if (gui.Button("+5s".AsSpan(), buttonSize))
            _playbackService.SkipForward();
    }

    private void DrawSpeedControl(ImGui gui)
    {
        var speed = _speed;
        var rowHeight = gui.GetRowHeight();

        gui.SliderHeader("Speed".AsSpan(), speed, "0.00x".AsSpan());

        ImSize speedSize = new Vector2(gui.GetLayoutWidth(), rowHeight);
        if (gui.Slider(ref speed, 0.25f, 4f, speedSize, 0.05f))
        {
            _speed = speed;
            _playbackService.SetSpeed(speed);
        }
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f)
            seconds = 0f;

        var minutes = (int)(seconds / 60f);
        var remainingSeconds = seconds - minutes * 60f;
        return $"{minutes:00}:{remainingSeconds:00.000}";
    }
}
