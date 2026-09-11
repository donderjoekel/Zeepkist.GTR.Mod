using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public sealed class LeaderboardPageSnapshot
{
    public string LevelName { get; set; }
    public int TotalRecords { get; set; }
    public IReadOnlyList<LeaderboardRecord> Records { get; set; } = new List<LeaderboardRecord>();
}
