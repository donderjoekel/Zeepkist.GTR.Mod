using System;
using Imui.Controls;
using Imui.Core;
using Imui.Rendering;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using UnityEngine;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GhostTimelineDrawer : IZeepGUIDrawer
{
    private readonly PhotoModeTimelineService _photoModeTimelineService;
    private readonly GhostPlaybackService _playbackService;
    private readonly GhostTimelineState _timelineState;

    private bool _mouseOverWindow;
    private float _scrubTime;
    private float _speed = 1f;
    private bool _isScrubbing;

    public GhostTimelineDrawer(
        PhotoModeTimelineService photoModeTimelineService,
        GhostPlaybackService playbackService,
        GhostTimelineState timelineState)
    {
        _photoModeTimelineService = photoModeTimelineService;
        _playbackService = playbackService;
        _timelineState = timelineState;
    }

    public void OnZeepGUI(ImGui gui)
    {
        if (!_timelineState.IsVisible || !_photoModeTimelineService.IsTimelineAvailable)
            return;

        var open = true;
        ImSize windowSize = new Vector2(960f, 128f);
        if (!gui.BeginWindow("GTR Timeline", ref open, ref _mouseOverWindow, windowSize))
            return;

        try
        {
            DrawTimeline(gui);
        }
        finally
        {
            gui.EndWindow();
        }

        if (!open)
            _timelineState.SetVisible(false);
    }

    private void DrawTimeline(ImGui gui)
    {
        var duration = _playbackService.Duration;
        if (duration <= 0f)
        {
            gui.Text("No ghost playback data available".AsSpan(),
                new Color32(255, 255, 255, 255));
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
