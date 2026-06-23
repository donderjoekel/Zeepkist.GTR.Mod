using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Patching.Patches;
using ZeepSDK.Multiplayer;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class SpectatorModeService : IEagerService
{
    private readonly GhostPlayer _ghostPlayer;
    private readonly GhostPlaybackService _ghostPlaybackService;

    public bool IsSpectatorActive { get; private set; }

    public bool IsTimelineAvailable =>
        !MultiplayerApi.IsPlayingOnline &&
        IsSpectatorActive &&
        _ghostPlayer.GetLoadedGhostIds().Count > 0;

    public SpectatorModeService(GhostPlayer ghostPlayer, GhostPlaybackService ghostPlaybackService)
    {
        _ghostPlayer = ghostPlayer;
        _ghostPlaybackService = ghostPlaybackService;

        SpectatorCameraUi_OnOpen.Enabled += OnSpectatorEnabled;
        SpectatorCameraUi_OnClose.Disabled += OnSpectatorDisabled;
    }

    private void OnSpectatorEnabled()
    {
        IsSpectatorActive = true;
    }

    private void OnSpectatorDisabled()
    {
        IsSpectatorActive = false;
        _ghostPlaybackService.Pause();
    }
}
