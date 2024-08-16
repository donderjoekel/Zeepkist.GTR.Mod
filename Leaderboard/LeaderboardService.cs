using TNRD.Zeepkist.GTR.Core;
using ZeepSDK.Leaderboard;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class LeaderboardService : IEagerService
{
    public LeaderboardService(OnlineLeaderboardTab onlineLeaderboardTab, OfflineLeaderboardTab offlineLeaderboardTab)
    {
        LeaderboardApi.AddTab(onlineLeaderboardTab);
        LeaderboardApi.AddTab(offlineLeaderboardTab);
    }
}
