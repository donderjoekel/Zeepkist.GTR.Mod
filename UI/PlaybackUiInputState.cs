using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;

namespace TNRD.Zeepkist.GTR.UI;

public class PlaybackUiInputState : IEagerService
{
    private readonly PlayerLoopSubscription _updateSubscription;

    public bool IsPointerOverGtrWindow { get; private set; }

    public PlaybackUiInputState(PlayerLoopService playerLoopService)
    {
        _updateSubscription = playerLoopService.SubscribeUpdate(ResetPointerOverGtrWindow);
    }

    public void NotifyPointerOverGtrWindow()
    {
        IsPointerOverGtrWindow = true;
    }

    private void ResetPointerOverGtrWindow()
    {
        IsPointerOverGtrWindow = false;
    }
}
