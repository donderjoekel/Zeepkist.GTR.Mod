using TNRD.Zeepkist.GTR.Core;
using ZeepSDK.Multiplayer;
using ZeepSDK.PhotoMode;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class PhotoModeTimelineService : IEagerService
{
    private readonly GhostPlayer _ghostPlayer;
    private readonly GhostPlaybackService _ghostPlaybackService;

    public bool IsPhotoModeActive { get; private set; }

    public bool IsTimelineAvailable =>
        !MultiplayerApi.IsPlayingOnline &&
        IsPhotoModeActive &&
        _ghostPlayer.GetLoadedGhostIds().Count > 0;

    public PhotoModeTimelineService(GhostPlayer ghostPlayer, GhostPlaybackService ghostPlaybackService)
    {
        _ghostPlayer = ghostPlayer;
        _ghostPlaybackService = ghostPlaybackService;

        PhotoModeApi.PhotoModeEntered += OnPhotoModeEntered;
        PhotoModeApi.PhotoModeExited += OnPhotoModeExited;

        if (PlayerManager.Instance?.currentMaster != null)
            IsPhotoModeActive = PlayerManager.Instance.currentMaster.isPhotoMode;
    }

    private void OnPhotoModeEntered()
    {
        IsPhotoModeActive = true;
    }

    private void OnPhotoModeExited()
    {
        IsPhotoModeActive = false;
        _ghostPlaybackService.Pause();
    }
}
