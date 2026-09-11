using System;
using System.Globalization;
using TNRD.Zeepkist.GTR.Utilities;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public static class LeaderboardTextFormatter
{
    private static readonly TimeSpan RelativeDateLimit = TimeSpan.FromDays(365);
    private static readonly TimeSpan FadeStart = TimeSpan.FromHours(1);
    private static readonly TimeSpan FadeEnd = TimeSpan.FromDays(365);

    public static string PrefixRecordDate(string originalText, string dateCreated, DateTimeOffset now)
    {
        if (!DateTimeOffset.TryParse(
                dateCreated,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out DateTimeOffset created))
            return originalText;

        TimeSpan age = now - created;

        if (age < TimeSpan.Zero)
            age = TimeSpan.Zero;

        string text = FormatAge(created, age);
        string color = FormatAgeColor(age);
        string padding = originalText.Length > 0 ? " " : string.Empty;

        return $"{originalText}<br><size=50%><color={color}>{text}</color></size>";
    }

    public static string FormatTitle(string levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
            return "GTR Records";

        return $"{EscapeRichText(levelName)}\n<size=50%>GTR records on zeepki.st</size>";
    }

    public static string FormatAgeColor(TimeSpan age)
    {
        if (age <= FadeStart)
            return TextColour.Yellow.ToHex();
        if (age >= FadeEnd)
            return TextColour.White.ToHex();

        double progress = (age - FadeStart).TotalSeconds / (FadeEnd - FadeStart).TotalSeconds;

        return TextColour.Yellow.InterpolateTo(TextColour.White, progress);
    }

    public static string EscapeRichText(string value)
    {
        return value?.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private static string FormatAge(DateTimeOffset created, TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
            return "just now";
        if (age < TimeSpan.FromHours(1))
            return FormatRelative((int)age.TotalMinutes, "minute");
        if (age < TimeSpan.FromDays(1))
            return FormatRelative((int)age.TotalHours, "hour");
        if (age < TimeSpan.FromDays(30))
            return FormatRelative((int)age.TotalDays, "day");
        if (age < TimeSpan.FromDays(365))
            return FormatRelative((int)(age.TotalDays / 30), "month");

        return created.ToLocalTime().ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
    }

    private static string FormatRelative(int value, string unit)
    {
        return $"{value} {unit}{(value == 1 ? string.Empty : "s")} ago";
    }
}
