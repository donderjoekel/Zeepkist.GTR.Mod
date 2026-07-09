using System;
using Imui.Controls;
using Imui.Core;
using Imui.IO.Events;
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
    private const int PlaybackContentRows = 5;
    private readonly PhotoModeTimelineService _photoModeTimelineService;
    private readonly GhostPlaybackService _playbackService;
    private readonly GhostTimelineState _timelineState;
    private readonly PlaybackUiInputState _playbackUiInputState;

    private bool _mouseOverWindow;
    private float _scrubTime;
    private float _speed = 1f;
    private bool _isScrubbing;
    private float _playbackWindowHeight;

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

    private ImSize GetTimelineWindowSize(ImGui gui, bool hasPlaybackData)
    {
        var height = hasPlaybackData
            ? GetPlaybackWindowHeight(gui)
            : GetEmptyWindowHeight(gui);

        var width = DefaultWindowWidth;
        var windowId = gui.GetControlId(WindowTitle.AsSpan());
        if (gui.WindowManager.TryFindWindow(windowId) >= 0)
            width = gui.WindowManager.GetWindowState(windowId).Rect.W;

        return new Vector2(width, height);
    }

    private float GetPlaybackWindowHeight(ImGui gui)
    {
        if (_playbackWindowHeight > 0f)
            return _playbackWindowHeight;

        var contentHeight = gui.GetRowsHeightWithSpacing(PlaybackContentRows);
        var windowPadding = gui.Style.Window.ContentPadding.Vertical;
        _playbackWindowHeight = contentHeight + ImWindow.GetTitleBarHeight(gui) + windowPadding;
        return _playbackWindowHeight;
    }

    private static float GetEmptyWindowHeight(ImGui gui)
    {
        var contentHeight = gui.GetRowsHeightWithSpacing(1);
        var windowPadding = gui.Style.Window.ContentPadding.Vertical;
        return contentHeight + ImWindow.GetTitleBarHeight(gui) + windowPadding;
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
        var scrubTime = currentTime;
        var scrubberChanged = DrawTimeScrubBar(gui, ref scrubTime, duration);
        _isScrubbing = scrubberChanged || gui.IsControlActive(gui.LastControl);

        if (scrubberChanged)
        {
            _scrubTime = scrubTime;
            _playbackService.Seek(scrubTime);
        }
    }

    private static bool DrawTimeScrubBar(ImGui gui, ref float time, float duration)
    {
        const float step = 0.01f;

        gui.AddSpacingIfLayoutFrameNotEmpty();

        var rowHeight = gui.GetRowHeight();
        var rect = gui.AddSingleRowRect(new ImSize(gui.GetLayoutWidth(), rowHeight));
        var id = gui.GetNextControlId();
        var hovered = gui.IsControlHovered(id);
        var active = gui.IsControlActive(id);

        var normValue = duration > 0f ? Mathf.Clamp01(time / duration) : 0f;
        var changed = false;

        ref readonly var trackStyle = ref active || hovered
            ? ref gui.Style.Slider.Selected
            : ref gui.Style.Slider.Normal;
        gui.Box(rect, in trackStyle);

        if (normValue > 0f)
        {
            var fillRect = rect;
            fillRect.W *= normValue;
            var fillColor = hovered || active
                ? gui.Style.Slider.Fill.FrontColor
                : gui.Style.Slider.Fill.BackColor;
            gui.Canvas.Rect(fillRect, fillColor, gui.Style.Slider.Normal.BorderRadius);
        }

        var timeDisplay = $"{FormatTime(time)} / {FormatTime(duration)}";
        var fontSize = gui.GetFontSizeForContainerHeight(rect.H * 0.75f);
        var textSettings = new ImTextSettings(fontSize, 0.5f, 0.5f);
        gui.Canvas.Text(timeDisplay.AsSpan(), gui.Style.Text.Color, rect, in textSettings);

        gui.RegisterControl(id, rect);

        if (gui.IsReadOnly)
            return false;

        ref readonly var evt = ref gui.Input.MouseEvent;
        switch (evt.Type)
        {
            case ImMouseEventType.Down or ImMouseEventType.BeginDrag when evt.LeftButton && hovered:
                normValue = Mathf.Clamp01(rect.GetNormalPositionAtPoint(gui.Input.MousePosition).x);
                changed = true;
                gui.SetActiveControl(id, ImControlFlag.Draggable);
                gui.Input.UseMouseEvent();
                break;
            case ImMouseEventType.Drag when active:
                normValue = Mathf.Clamp01(rect.GetNormalPositionAtPoint(gui.Input.MousePosition).x);
                changed = true;
                gui.Input.UseMouseEvent();
                break;
            case ImMouseEventType.Up when active:
                gui.ResetActiveControl();
                break;
        }

        if (!changed)
            return false;

        time = Mathf.Clamp(normValue * duration, 0f, duration);

        var precision = 1.0f / step;
        time = Mathf.Round(time * precision) / precision;

        return true;
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
