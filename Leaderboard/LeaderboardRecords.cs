using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class LeaderboardRecords
{
    public List<LeaderboardRecord> Records { get; set; }
    public double LevelPoints { get; set; }
    public int TotalUsers { get; set; }
}
