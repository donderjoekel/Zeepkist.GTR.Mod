using System;
using Imui.Controls;
using Imui.Core;
using Imui.IO.Events;
using Imui.Rendering;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.UI;
using UnityEngine;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GhostTimelineDrawer : IZeepGUIDrawer
{
    private const string WindowTitle = "GTR Playback";
    private const float DefaultWindowWidth = 960f;
    private const float SpeedStep = 0.1f;
    private const float SpeedMin = 0.1f;
    private const float SpeedMax = 4f;
    private const float SpeedButtonWidth = 56f;
    private const float ScrubStep = 0.01f;
    private const ImWindowFlag WindowFlags =
        ImWindowFlag.NoTitleBar | ImWindowFlag.NoCloseButton | ImWindowFlag.NoResizing;

    private readonly PhotoModeTimelineService _photoModeTimelineService;
    private readonly GhostPlaybackService _playbackService;
    private readonly ConfigService _configService;
    private readonly GhostTimelineState _timelineState;
    private readonly PlaybackUiInputState _playbackUiInputState;

    private bool _mouseOverWindow;
    private float _scrubTime;
    private float _speed = 1f;
    private bool _isScrubbing;
    private float _playbackWindowHeight;
    private const float WindowHeightVersion = 2f;
    private float _windowHeightVersion;

    public GhostTimelineDrawer(
        PhotoModeTimelineService photoModeTimelineService,
        GhostPlaybackService playbackService,
        ConfigService configService,
        GhostTimelineState timelineState,
        PlaybackUiInputState playbackUiInputState)
    {
        _photoModeTimelineService = photoModeTimelineService;
        _playbackService = playbackService;
        _configService = configService;
        _timelineState = timelineState;
        _playbackUiInputState = playbackUiInputState;
    }

    public void OnZeepGUI(ImGui gui)
    {
        _playbackUiInputState.BeginUiFrame();

        if (!_timelineState.ShouldShow || !_photoModeTimelineService.IsTimelineAvailable)
            return;

        var duration = _playbackService.Duration;
        var open = true;
        ImSize windowSize = GetTimelineWindowSize(gui, duration > 0f);
        ImRect windowRect = ImWindowPlacement.GetRect(
            gui,
            WindowTitle.AsSpan(),
            windowSize.Width,
            windowSize.Height,
            ImWindowAnchor.BottomCenter);
        if (!gui.BeginWindow(WindowTitle, ref open, ref _mouseOverWindow, windowRect, WindowFlags))
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
            _configService.ShowTimeline.Value = false;
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
        if (_playbackWindowHeight > 0f && Mathf.Approximately(_windowHeightVersion, WindowHeightVersion))
            return _playbackWindowHeight;

        var contentHeight = gui.GetRowHeight();
        var windowPadding = gui.Style.Window.ContentPadding.Vertical;
        var borderThickness = gui.Style.Window.Box.BorderThickness * 2f;
        _playbackWindowHeight = contentHeight + windowPadding + borderThickness;
        _windowHeightVersion = WindowHeightVersion;
        return _playbackWindowHeight;
    }

    private static float GetEmptyWindowHeight(ImGui gui)
    {
        var contentHeight = gui.GetRowHeight();
        var windowPadding = gui.Style.Window.ContentPadding.Vertical;
        return contentHeight + windowPadding;
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

        _speed = _playbackService.Speed;

        using (gui.Horizontal())
        {
            DrawPlayPauseButton(gui);
            DrawTimeScrubber(gui, _scrubTime, duration);
            DrawSpeedButton(gui);
        }
    }

    private void DrawPlayPauseButton(ImGui gui)
    {
        var rowHeight = gui.GetRowHeight();
        var label = _playbackService.IsPlaying ? "⏸" : "▶";
        ImSize buttonSize = new Vector2(rowHeight, rowHeight);

        if (gui.Button(label.AsSpan(), buttonSize))
            _playbackService.TogglePlayPause();
    }

    private void DrawTimeScrubber(ImGui gui, float currentTime, float duration)
    {
        var spacing = gui.Style.Layout.Spacing;
        gui.AddSpacingIfLayoutFrameNotEmpty();
        var scrubWidth = Mathf.Max(0f, gui.GetLayoutWidth() - SpeedButtonWidth - spacing);
        var scrubTime = currentTime;
        var allowScrollStep = !_playbackService.IsPlaying;
        var scrubberChanged = DrawTimeScrubBar(gui, ref scrubTime, duration, scrubWidth, allowScrollStep, out var frameStepped);
        _isScrubbing = scrubberChanged || gui.IsControlActive(gui.LastControl);

        if (scrubberChanged)
        {
            _scrubTime = scrubTime;
            if (!frameStepped)
                _playbackService.Seek(scrubTime);
        }
    }

    private bool DrawTimeScrubBar(
        ImGui gui,
        ref float time,
        float duration,
        float width,
        bool allowScrollStep,
        out bool frameStepped)
    {
        frameStepped = false;
        var rowHeight = gui.GetRowHeight();
        var rect = gui.AddSingleRowRect(new ImSize(width, rowHeight));
        var id = gui.GetNextControlId();
        var hovered = gui.IsControlHovered(id);
        var active = gui.IsControlActive(id);

        var normValue = duration > 0f ? Mathf.Clamp01(time / duration) : 0f;
        var changed = false;

        ref readonly var trackStyle = ref hovered || active
            ? ref gui.Style.Slider.Selected
            : ref gui.Style.Slider.Normal;
        gui.Box(rect, in trackStyle);

        if (normValue > 0f)
        {
            var fillRect = new ImRect(rect.X, rect.Y, rect.W * normValue, rect.H);
            gui.Box(fillRect, gui.Style.Slider.Fill);
        }

        var timeDisplay = $"{FormatTime(time)} / {FormatTime(duration)}";
        var fontSize = gui.GetFontSizeForContainerHeight(rect.H * 0.75f);
        var textSettings = new ImTextSettings(fontSize, 0.5f, 0.5f);
        gui.Canvas.Text(timeDisplay.AsSpan(), gui.Style.Window.TitleBar.FrontColor, rect, in textSettings);

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

        if (!changed && allowScrollStep && hovered && evt.Type == ImMouseEventType.Scroll)
        {
            var direction = evt.Delta.y > 0f ? 1 : -1;
            if (_configService.InvertTimelineScrubScroll.Value)
                direction = -direction;

            TimelineScrollStep.GetScrubScrollStep(
                IsAltHeld(),
                IsShiftHeld(),
                IsControlHeld(),
                out var useFrames,
                out var seconds,
                out var frameCount);

            if (useFrames)
                _playbackService.StepFrames(direction, frameCount);
            else
                _playbackService.StepTime(direction * seconds);

            time = _playbackService.CurrentTime;
            changed = true;
            frameStepped = true;
            gui.Input.UseMouseEvent();
        }

        if (!changed)
            return false;

        if (!frameStepped)
        {
            time = Mathf.Clamp(normValue * duration, 0f, duration);

            var precision = 1.0f / ScrubStep;
            time = Mathf.Round(time * precision) / precision;
        }

        return true;
    }

    private void DrawSpeedButton(ImGui gui)
    {
        var rowHeight = gui.GetRowHeight();
        var label = $"{_speed:0.0}x";
        var id = gui.GetNextControlId();

        gui.AddSpacingIfLayoutFrameNotEmpty();
        var rect = gui.AddSingleRowRect(new ImSize(SpeedButtonWidth, rowHeight));

        gui.Button(id, label.AsSpan(), rect, out _);
        var hovered = gui.IsControlHovered(id);

        if (hovered)
            _playbackUiInputState.NotifyPointerOverSpeedControl();

        if (gui.IsReadOnly)
            return;

        ref readonly var evt = ref gui.Input.MouseEvent;
        if (evt.Type != ImMouseEventType.Scroll || !hovered)
            return;

        var delta = evt.Delta.y > 0f ? SpeedStep : -SpeedStep;
        var speed = Mathf.Clamp(_speed + delta, SpeedMin, SpeedMax);
        speed = Mathf.Round(speed * 10f) / 10f;

        if (Mathf.Approximately(speed, _speed))
            return;

        _speed = speed;
        _playbackService.SetSpeed(speed);
        gui.Input.UseMouseEvent();
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f)
            seconds = 0f;

        var minutes = (int)(seconds / 60f);
        var remainingSeconds = seconds - minutes * 60f;
        return $"{minutes:00}:{remainingSeconds:00.000}";
    }

    private static bool IsShiftHeld() =>
        Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    private static bool IsControlHeld() =>
        Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    private static bool IsAltHeld() =>
        Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
}
