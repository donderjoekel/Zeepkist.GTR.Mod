namespace TNRD.Zeepkist.GTR.UI.Timeline;

public class GhostTimelineState
{
    public bool IsVisible { get; private set; }

    public void ToggleVisible()
    {
        IsVisible = !IsVisible;
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
    }
}
