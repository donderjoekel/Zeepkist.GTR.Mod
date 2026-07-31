using System;
using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.UI.Tournaments;

public sealed class TournamentStandingEntry
{
    public int Rank { get; init; }
    public string PlayerName { get; init; }
    public float Time { get; init; }
    public int Points { get; init; }
}

public sealed class TournamentViewModel
{
    public int Id { get; init; }
    public int Type { get; init; }
    public string Slug { get; init; }
    public DateTime StartAt { get; init; }
    public DateTime EndAt { get; init; }
    public string LevelName { get; init; }
    public string LevelAuthor { get; init; }
    public string LevelUid { get; init; }
    public ulong WorkshopId { get; init; }
    public string ImageUrl { get; init; }
    public float ValidationTimeAuthor { get; init; }
    public float ValidationTimeGold { get; init; }
    public float ValidationTimeSilver { get; init; }
    public float ValidationTimeBronze { get; init; }
    public int StandingCount { get; init; }
    public IReadOnlyList<TournamentStandingEntry> Standings { get; init; } =
        Array.Empty<TournamentStandingEntry>();

    public string LobbyName => $"[{Slug}] {LevelName}";
    public string TypeLabel => Type switch
    {
        0 => "Track of the Week",
        _ => $"Type {Type}"
    };
}
