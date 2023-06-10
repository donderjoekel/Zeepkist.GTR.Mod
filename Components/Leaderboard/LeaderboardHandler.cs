using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Mod.Components.Leaderboard.Pages;
using TNRD.Zeepkist.GTR.Mod.Patches;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Mod.Components.Leaderboard;

internal class LeaderboardHandler : MonoBehaviourWithLogging
{
    private readonly List<ILeaderboardTab> pages = new List<ILeaderboardTab>();
    private int currentPageIndex;
    private ILeaderboardTab CurrentLeaderboardTab => pages[currentPageIndex];

    private void Start()
    {
        pages.Add(new RoundLeaderboardTab());
        pages.Add(new ChampionshipLeaderboardTab());
        pages.Add(new GtrLeaderboardTab());

        OnlineTabLeaderboardUI_OnOpen.OnOpen += OnOpen;
        OnlineTabLeaderboardUI_OnClose.OnClose += OnClose;
        OnlineTabLeaderboardUI_Update.Update += OnUpdate;
    }

    private void OnOpen(OnlineTabLeaderboardUI sender)
    {
        sender.PauseHandler.Pause();
        currentPageIndex = 0;
        CurrentLeaderboardTab.Enable(sender);
        CurrentLeaderboardTab.Draw();
    }

    private void OnClose(OnlineTabLeaderboardUI sender)
    {
        sender.PauseHandler.Unpause();
        CurrentLeaderboardTab.Disable();
    }

    private void OnUpdate(OnlineTabLeaderboardUI sender)
    {
        if (!ZeepkistNetwork.IsConnected || ZeepkistNetwork.CurrentLobby == null)
        {
            return;
        }

        if (sender.LeaderboardAction.buttonDown || sender.EscapeAction.buttonDown)
        {
            sender.Close(true);
        }

        if (sender.SwitchAction.buttonDown)
        {
            CurrentLeaderboardTab.Disable();
            currentPageIndex = (currentPageIndex + 1) % pages.Count;
            CurrentLeaderboardTab.Enable(sender);
            CurrentLeaderboardTab.Draw();
        }

        string timeNoMilliSeconds =
            (ZeepkistNetwork.CurrentLobby.RoundTime -
             (ZeepkistNetwork.Time - ZeepkistNetwork.CurrentLobby.LevelLoadedAtTime)).GetFormattedTimeNoMilliSeconds();
        sender.timeLeftLeaderboard.text = ZeepkistNetwork.CurrentLobby.GameState == 0 ? timeNoMilliSeconds : "";

        if (sender.MenuLeftAction.buttonDown)
        {
            CurrentLeaderboardTab.GoToPreviousPage();
            CurrentLeaderboardTab.Draw();
        }

        if (sender.MenuRightAction.buttonDown)
        {
            CurrentLeaderboardTab.GoToNextPage();
            CurrentLeaderboardTab.Draw();
        }
    }
}
