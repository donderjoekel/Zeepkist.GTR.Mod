using TNRD.Zeepkist.GTR.Leaderboard;
using TNRD.Zeepkist.GTR.Utilities;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class LeaderboardTextFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(30, "just now")]
    [InlineData(60, "1 minute ago")]
    [InlineData(3599, "59 minutes ago")]
    [InlineData(3600, "1 hour ago")]
    [InlineData(86399, "23 hours ago")]
    [InlineData(86400, "1 day ago")]
    [InlineData(2505600, "29 days ago")]
    public void FormatsRelativeAgeBoundaries(int ageSeconds, string expected)
    {
        string result = LeaderboardTextFormatter.PrefixRecordDate(
            "player",
            Now.AddSeconds(-ageSeconds).ToString("O"),
            Now);

        Assert.Contains($">{expected}</color>", result);
    }

    [Theory]
    [InlineData(2592000, "1 month ago")]
    [InlineData(5184000, "2 months ago")]
    [InlineData(31449600, "12 months ago")]
    public void FormatsMonthsThroughFirstYear(int ageSeconds, string expected)
    {
        string result = LeaderboardTextFormatter.PrefixRecordDate(
            "player",
            Now.AddSeconds(-ageSeconds).ToString("O"),
            Now);

        Assert.Contains($">{expected}</color>", result);
    }

    [Fact]
    public void UsesFixedLocalDateAtOneYear()
    {
        DateTimeOffset created = Now.AddDays(-365);

        string result = LeaderboardTextFormatter.PrefixRecordDate("player", created.ToString("O"), Now);

        Assert.Contains(created.ToLocalTime().ToString("yyyy/MM/dd"), result);
    }

    [Fact]
    public void FutureTimestampClampsToJustNow()
    {
        string result = LeaderboardTextFormatter.PrefixRecordDate(
            "player",
            Now.AddMinutes(5).ToString("O"),
            Now);

        Assert.Contains(">just now</color>", result);
    }

    [Fact]
    public void MalformedTimestampLeavesMarkupUnchanged()
    {
        Assert.Equal("<link=\"1\">player</link>",
            LeaderboardTextFormatter.PrefixRecordDate("<link=\"1\">player</link>", "bad", Now));
    }

    [Fact]
    public void AppendsSmallDateAfterText()
    {
        string result = LeaderboardTextFormatter.PrefixRecordDate(
            "<color=#fff><link=\"1\">player</link></color>",
            Now.AddMinutes(-2).ToString("O"),
            Now);

        Assert.StartsWith("<color=#fff><link=\"1\">player</link></color><br><size=50%>", result);
        Assert.EndsWith("</color></size>", result);
    }

    [Fact]
    public void FadesFromYellowToWhite()
    {
        Assert.Equal(TextColour.Yellow.ToHex(), LeaderboardTextFormatter.FormatAgeColor(TimeSpan.FromHours(1)));
        Assert.NotEqual(TextColour.Yellow.ToHex(), LeaderboardTextFormatter.FormatAgeColor(TimeSpan.FromDays(180)));
        Assert.NotEqual(TextColour.White.ToHex(), LeaderboardTextFormatter.FormatAgeColor(TimeSpan.FromDays(180)));
        Assert.Equal(TextColour.White.ToHex(), LeaderboardTextFormatter.FormatAgeColor(TimeSpan.FromDays(365)));
    }

    [Fact]
    public void FormatsEscapedTwoLineTitleAndFallback()
    {
        Assert.Equal("GTR Records", LeaderboardTextFormatter.FormatTitle(null));
        Assert.Equal(
            "A &lt;Level&gt; &amp; More\n<size=50%>GTR records on zeepki.st</size>",
            LeaderboardTextFormatter.FormatTitle("A <Level> & More"));
    }
}
