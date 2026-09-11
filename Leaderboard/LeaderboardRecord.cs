namespace TNRD.Zeepkist.GTR.Leaderboard;

public class LeaderboardRecord
{
    public string SteamId { get; set; }
    public string SteamName { get; set; }
    public double Time { get; set; }
    public string DateCreated { get; set; }
    public int? LevelPosition { get; set; }
    public double? LevelDecayedPoints { get; set; }
}
