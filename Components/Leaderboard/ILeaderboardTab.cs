namespace TNRD.Zeepkist.GTR.Mod.Components.Leaderboard;

public interface ILeaderboardTab
{
    void Enable(OnlineTabLeaderboardUI sender);
    void Disable();
    void GoToPreviousPage();
    void GoToNextPage();
    void Draw();
}
