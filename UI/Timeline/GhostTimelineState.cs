namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GhostTimelineState
{
    public bool IsVisible { get; private set; }
    public bool IsHiddenByPauseMenu { get; private set; }
    public bool ShouldShow => IsVisible && !IsHiddenByPauseMenu;

    public void ToggleVisible()
    {
        IsVisible = !IsVisible;
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
    }

    public void SetHiddenByPauseMenu(bool hidden)
    {
        IsHiddenByPauseMenu = hidden;
    }
}
