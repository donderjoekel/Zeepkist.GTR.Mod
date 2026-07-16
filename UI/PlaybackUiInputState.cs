using TNRD.Zeepkist.GTR.Core;

namespace TNRD.Zeepkist.GTR.UI;

public class PlaybackUiInputState : IEagerService
{
    public bool IsPointerOverGtrWindow { get; private set; }

    public bool IsPointerOverSpeedControl { get; private set; }

    public void BeginUiFrame()
    {
        IsPointerOverGtrWindow = false;
        IsPointerOverSpeedControl = false;
    }

    public void NotifyPointerOverGtrWindow()
    {
        IsPointerOverGtrWindow = true;
    }

    public void NotifyPointerOverSpeedControl()
    {
        IsPointerOverSpeedControl = true;
    }
}
