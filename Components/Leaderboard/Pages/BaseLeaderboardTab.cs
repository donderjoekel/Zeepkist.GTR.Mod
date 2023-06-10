using BepInEx.Logging;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Mod.Components.Leaderboard.Pages;

internal abstract class BaseLeaderboardTab : ILeaderboardTab
{
    protected ManualLogSource Logger { get; private set; }
    protected OnlineTabLeaderboardUI Instance { get; private set; }
    protected int CurrentPage { get; private set; }
    protected int MaxPages { get; set; }

    protected bool IsActive { get; private set; }

    protected BaseLeaderboardTab()
    {
        Logger = EntryPoint.CreateLogger(GetType().Name);
    }

    /// <inheritdoc />
    public void Enable(OnlineTabLeaderboardUI sender)
    {
        IsActive = true;
        Instance = sender;

        Instance.playersLeaderboard.text = I2.Loc.LocalizationManager.GetTranslation("Online/Leaderboard/PlayerCount")
            .Replace("{[PLAYERS]}", ZeepkistNetwork.PlayerList.Count.ToString())
            .Replace("{[MAXPLAYERS]}", ZeepkistNetwork.CurrentLobby.MaxPlayerCount.ToString());

        Instance.leaderboardTitle.text = GetLeaderboardTitle();

        CurrentPage = 0;
        UpdatePageNumber();

        OnEnable();
    }

    /// <inheritdoc />
    public void Disable()
    {
        OnDisable();
        IsActive = false;
    }

    /// <inheritdoc />
    public void GoToPreviousPage()
    {
        if (!IsActive)
            return;

        CurrentPage = CurrentPage - 1 < 0 ? MaxPages : CurrentPage - 1;
        UpdatePageNumber();
    }

    /// <inheritdoc />
    public void GoToNextPage()
    {
        if (!IsActive)
            return;

        CurrentPage = CurrentPage + 1 > MaxPages ? 0 : CurrentPage + 1;
        UpdatePageNumber();
    }

    /// <inheritdoc />
    public void Draw()
    {
        if (!IsActive)
            return;

        ClearLeaderboard();
        OnDraw();
    }

    protected void UpdatePageNumber()
    {
        Instance.Page.text = I2.Loc.LocalizationManager.GetTranslation("Online/Lobby/Page")
            .Replace("{[PAGE]}", (CurrentPage + 1).ToString() + "/" + (MaxPages + 1).ToString());
    }

    private void ClearLeaderboard()
    {
        foreach (GUI_OnlineLeaderboardPosition item in Instance.leaderboard_tab_positions)
        {
            item.DrawLeaderboard(0, "");
            item.time.text = "";
            item.position.gameObject.SetActive(false);
            item.pointsCurrent.text = "";
            item.pointsWon.text = "";
        }
    }

    protected abstract string GetLeaderboardTitle();
    protected abstract void OnEnable();
    protected abstract void OnDisable();
    protected abstract void OnDraw();
}
