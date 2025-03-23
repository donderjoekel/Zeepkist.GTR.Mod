using System;
using TNRD.Zeepkist.GTR.Core;
using UnityEngine;
using ZeepSDK.Leaderboard;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class LeaderboardService : IEagerService
{
    public LeaderboardService(OnlineLeaderboardTab onlineLeaderboardTab, OfflineLeaderboardTab offlineLeaderboardTab)
    {
        try
        {
            LeaderboardApi.AddTab(offlineLeaderboardTab);
            LeaderboardApi.AddTab(onlineLeaderboardTab);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
