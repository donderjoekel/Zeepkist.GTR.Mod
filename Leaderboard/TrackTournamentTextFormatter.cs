using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public static class TrackTournamentTextFormatter
{
    private static readonly Regex WeeklySlug = new(@"^(\d{4})-w(\d{2})$", RegexOptions.IgnoreCase);
    private static readonly Regex MonthlySlug = new(@"^(\d{4})-(\d{2})$");

    public static string FormatTitle(TrackTournamentDescriptor tournament, DateTimeOffset now)
    {
        if (tournament == null)
            return "Track Tournament";

        string heading = tournament.Type == TrackTournamentType.Weekly
            ? "Track of the Week"
            : "Track of the Month";
        string period = FormatPeriod(tournament.Type, tournament.Slug);
        bool live = tournament.StartAt <= now && now < tournament.EndAt;
        return $"{heading}\n{period}{(live ? " (Live)" : string.Empty)}";
    }

    public static string FormatPeriod(TrackTournamentType type, string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return string.Empty;

        if (type == TrackTournamentType.Weekly)
        {
            Match match = WeeklySlug.Match(slug);
            if (match.Success && int.TryParse(match.Groups[2].Value, out int week) && week is >= 1 and <= 53)
                return $"{match.Groups[1].Value} Week {week}";
            return slug;
        }

        Match monthly = MonthlySlug.Match(slug);
        if (!monthly.Success ||
            !int.TryParse(monthly.Groups[1].Value, out int year) ||
            !int.TryParse(monthly.Groups[2].Value, out int month) ||
            month is < 1 or > 12)
        {
            return slug;
        }

        return new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
    }
}
