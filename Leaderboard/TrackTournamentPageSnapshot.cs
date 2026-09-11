using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public sealed class TrackTournamentPageSnapshot
{
    public TrackTournamentDescriptor Tournament { get; set; }
    public int TotalRecords { get; set; }
    public IReadOnlyList<TrackTournamentStanding> Records { get; set; } =
        new List<TrackTournamentStanding>();
}
