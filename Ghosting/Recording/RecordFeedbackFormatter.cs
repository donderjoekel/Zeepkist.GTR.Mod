using System;
using System.Collections.Generic;
using System.Globalization;
using TNRD.Zeepkist.GTR.Utilities;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public enum RecordFeedbackKind
{
    None,
    PersonalBest,
    FirstPersonalBest,
    NewWorldRecord,
    ImprovedWorldRecord
}

public sealed class RecordFeedbackMessageData
{
    public RecordFeedbackKind Kind { get; set; }
    public string PreviousDelta { get; set; }
    public string NextDelta { get; set; }
    public bool WasFirstPersonalBest { get; set; }
    public int? PreviousPosition { get; set; }
    public int? Position { get; set; }
    public double? LevelDecayedPoints { get; set; }
    public double? PlayerDecayedPoints { get; set; }
}

public static class RecordFeedbackFormatter
{
    public static RecordFeedbackKind Classify(
        double submittedTime,
        double? previousPersonalBest,
        double? previousWorldRecord,
        string previousWorldRecordSteamId,
        string playerSteamId)
    {
        if (previousPersonalBest.HasValue && submittedTime >= previousPersonalBest.Value)
            return RecordFeedbackKind.None;

        bool isWorldRecord = !previousWorldRecord.HasValue || submittedTime < previousWorldRecord.Value;
        if (isWorldRecord)
        {
            return previousWorldRecord.HasValue &&
                   string.Equals(previousWorldRecordSteamId, playerSteamId, StringComparison.Ordinal)
                ? RecordFeedbackKind.ImprovedWorldRecord
                : RecordFeedbackKind.NewWorldRecord;
        }

        return previousPersonalBest.HasValue
            ? RecordFeedbackKind.PersonalBest
            : RecordFeedbackKind.FirstPersonalBest;
    }

    public static bool ShouldShow(
        RecordFeedbackKind kind,
        bool showPersonalBest,
        bool showNewWorldRecord,
        bool showImprovedWorldRecord)
    {
        return kind switch
        {
            RecordFeedbackKind.PersonalBest => showPersonalBest,
            RecordFeedbackKind.FirstPersonalBest => showPersonalBest,
            RecordFeedbackKind.NewWorldRecord => showNewWorldRecord,
            RecordFeedbackKind.ImprovedWorldRecord => showImprovedWorldRecord,
            _ => false
        };
    }

    public static RecordFeedbackKind ClassifyConfirmed(
        bool hadPreviousPersonalBest,
        bool isWorldRecord,
        bool previousWorldRecordOwnedByPlayer)
    {
        if (isWorldRecord)
        {
            return previousWorldRecordOwnedByPlayer
                ? RecordFeedbackKind.ImprovedWorldRecord
                : RecordFeedbackKind.NewWorldRecord;
        }

        return hadPreviousPersonalBest
            ? RecordFeedbackKind.PersonalBest
            : RecordFeedbackKind.FirstPersonalBest;
    }

    public static bool RequiresNextFastest(RecordFeedbackKind kind)
    {
        return kind is RecordFeedbackKind.PersonalBest or RecordFeedbackKind.FirstPersonalBest;
    }

    public static bool IsWorldRecordPosition(int? levelPosition)
    {
        return levelPosition == 1;
    }

    public static bool HasRankedProjection(bool matchingPersonalBest, int? position, double? levelPoints)
    {
        return matchingPersonalBest && position.HasValue && levelPoints.HasValue;
    }

    public static string Format(RecordFeedbackMessageData data)
    {
        List<string> lines = new();

        string improvement = data.Kind switch
        {
            RecordFeedbackKind.PersonalBest => "PB",
            RecordFeedbackKind.NewWorldRecord or RecordFeedbackKind.ImprovedWorldRecord => "WR",
            _ => null
        };

        if (improvement != null && !string.IsNullOrEmpty(data.PreviousDelta))
            lines.Add($"<size=75%>{improvement} improved by {TextColour.Pink.Wrap(data.PreviousDelta)}</size>");

        if (data.Position.HasValue && data.LevelDecayedPoints.HasValue)
        {
            string score = FormatScore(data);
            string position = FormatPosition(data.Position.Value);

            if (data.WasFirstPersonalBest)
            {
                lines.Add($"<size=75%>{position} <size=60%>({score})</size></size>");
            }
            else if (data.PreviousPosition.HasValue &&
                     data.PreviousPosition.Value > data.Position.Value)
            {
                lines.Add(
                    $"<size=75%>#{data.PreviousPosition.Value} -> {position} <size=60%>({score})</size></size>");
            }
        }

        if (!string.IsNullOrEmpty(data.NextDelta) &&
            data.Kind is RecordFeedbackKind.PersonalBest or RecordFeedbackKind.FirstPersonalBest)
        {
            lines.Add($"<size=50%>Gap to next player: {TextColour.Pink.Wrap(data.NextDelta)}</size>");
        }

        return string.Join("<br>", lines);
    }

    private static string FormatPosition(int position)
    {
        string value = $"#{position}";
        return position == 1 ? TextColour.Yellow.Wrap(value) : value;
    }

    private static string FormatScore(RecordFeedbackMessageData data)
    {
        string levelPoints = Math.Round(data.LevelDecayedPoints.Value)
            .ToString(CultureInfo.InvariantCulture);
        return TextColour.Yellow.Wrap($"{levelPoints}pts");
    }
}
