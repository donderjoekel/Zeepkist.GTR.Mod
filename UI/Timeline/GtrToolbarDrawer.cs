using Imui.Controls;
using Imui.Core;
using Imui.Rendering;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.UI.Config;
using UnityEngine;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GtrToolbarDrawer : IZeepToolbarDrawer
{
    private const float SpeedStep = 0.1f;
    private const float SpeedMin = 0.25f;
    private const float SpeedMax = 4f;

    private readonly PhotoModeTimelineService _photoModeTimelineService;
    private readonly GhostPlaybackService _playbackService;
    private readonly ConfigService _configService;
    private readonly GtrConfigToolbarDrawer _configToolbarDrawer;

    public GtrToolbarDrawer(
        PhotoModeTimelineService photoModeTimelineService,
        GhostPlaybackService playbackService,
        ConfigService configService)
    {
        _photoModeTimelineService = photoModeTimelineService;
        _playbackService = playbackService;
        _configService = configService;
        _configToolbarDrawer = new GtrConfigToolbarDrawer(configService);
    }

    public string MenuTitle => "GTR";

    public void DrawMenuItems(ImGui gui)
    {
        DrawTimelineMenu(gui);
        gui.Separator();
        _configToolbarDrawer.Draw(gui);
    }

    private void DrawTimelineMenu(ImGui gui)
    {
        if (!gui.BeginMenu("Timeline"))
            return;

        if (_photoModeTimelineService.IsPhotoModeGhostsAvailable)
        {
            if (gui.Menu("Show Timeline", _configService.ShowTimeline.Value))
                _configService.ShowTimeline.Value = !_configService.ShowTimeline.Value;

            gui.Separator();
            gui.Text($"Speed: {_playbackService.Speed:0.0}x");

            if (gui.Menu("Increase Speed"))
                AdjustSpeedClamped(SpeedStep);

            if (gui.Menu("Decrease Speed"))
                AdjustSpeedClamped(-SpeedStep);

            if (gui.Menu("Reset Speed"))
                _playbackService.ResetSpeed();
        }
        else
        {
            gui.Text("Not available outside photo mode",
                new Color32(128, 128, 128, 255));
        }

        gui.EndMenu();
    }

    private void AdjustSpeedClamped(float delta)
    {
        var speed = Mathf.Round((_playbackService.Speed + delta) * 10f) / 10f;
        speed = Mathf.Clamp(speed, SpeedMin, SpeedMax);
        _playbackService.SetSpeed(speed);
    }
}
