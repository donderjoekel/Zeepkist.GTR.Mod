using System;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public enum TrackTournamentType
{
    Weekly = 0,
    Monthly = 1
}

public sealed class TrackTournamentDescriptor
{
    public int Id { get; set; }
    public TrackTournamentType Type { get; set; }
    public string Slug { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
}
