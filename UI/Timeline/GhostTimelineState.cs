namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GhostTimelineState
{
    public bool IsVisible { get; private set; }
    public bool IsHiddenByOverlay { get; private set; }
    public bool ShouldShow => IsVisible && !IsHiddenByOverlay;

    public void ToggleVisible()
    {
        IsVisible = !IsVisible;
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
    }

    public void SetHiddenByOverlay(bool hidden)
    {
        IsHiddenByOverlay = hidden;
    }
}
