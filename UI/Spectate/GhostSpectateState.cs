namespace TNRD.Zeepkist.GTR.UI.Spectate;

public class GhostSpectateState
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
