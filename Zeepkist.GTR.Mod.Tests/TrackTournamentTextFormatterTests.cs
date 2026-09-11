using TNRD.Zeepkist.GTR.Leaderboard;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class TrackTournamentTextFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FormatsLiveWeeklyTitle()
    {
        Assert.Equal(
            "Track of the Week\n2026 Week 37 (Live)",
            TrackTournamentTextFormatter.FormatTitle(Tournament(
                TrackTournamentType.Weekly,
                "2026-w37",
                Now.AddDays(-1),
                Now.AddDays(1)), Now));
    }

    [Fact]
    public void FormatsPastMonthlyTitleWithInvariantMonthName()
    {
        Assert.Equal(
            "Track of the Month\nSeptember 2026",
            TrackTournamentTextFormatter.FormatTitle(Tournament(
                TrackTournamentType.Monthly,
                "2026-09",
                Now.AddMonths(-1),
                Now.AddSeconds(-1)), Now));
    }

    [Fact]
    public void EndBoundaryIsNotLive()
    {
        Assert.Equal(
            "Track of the Week\n2026 Week 37",
            TrackTournamentTextFormatter.FormatTitle(Tournament(
                TrackTournamentType.Weekly,
                "2026-w37",
                Now.AddDays(-7),
                Now), Now));
    }

    [Theory]
    [InlineData(TrackTournamentType.Weekly, "bad-week")]
    [InlineData(TrackTournamentType.Weekly, "2026-w00")]
    [InlineData(TrackTournamentType.Monthly, "2026-13")]
    public void MalformedSlugFallsBackToRawValue(TrackTournamentType type, string slug)
    {
        Assert.Equal(slug, TrackTournamentTextFormatter.FormatPeriod(type, slug));
    }

    private static TrackTournamentDescriptor Tournament(
        TrackTournamentType type,
        string slug,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        return new TrackTournamentDescriptor
        {
            Type = type,
            Slug = slug,
            StartAt = start,
            EndAt = end
        };
    }
}
