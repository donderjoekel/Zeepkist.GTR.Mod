using TNRD.Zeepkist.GTR.Ghosting.Recording;
using TNRD.Zeepkist.GTR.Utilities;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class RecordFeedbackFormatterTests
{
    [Fact]
    public void ClassifiesNonPersonalBest()
    {
        Assert.Equal(RecordFeedbackKind.None,
            RecordFeedbackFormatter.Classify(12, 11, 10, "other", "me"));
    }

    [Theory]
    [InlineData(RecordFeedbackKind.PersonalBest, 12.0, 13.0, 10.0, "other")]
    [InlineData(RecordFeedbackKind.FirstPersonalBest, 12.0, null, 10.0, "other")]
    [InlineData(RecordFeedbackKind.NewWorldRecord, 9.0, 12.0, 10.0, "other")]
    [InlineData(RecordFeedbackKind.ImprovedWorldRecord, 9.0, 10.0, 10.0, "me")]
    public void ClassifiesFeedbackKinds(
        RecordFeedbackKind expected,
        double submitted,
        double? personalBest,
        double? worldRecord,
        string worldRecordSteamId)
    {
        Assert.Equal(expected,
            RecordFeedbackFormatter.Classify(submitted, personalBest, worldRecord, worldRecordSteamId, "me"));
    }

    [Fact]
    public void FormatsCompactPersonalBestFeedback()
    {
        string result = RecordFeedbackFormatter.Format(new RecordFeedbackMessageData
        {
            Kind = RecordFeedbackKind.PersonalBest,
            PreviousDelta = "00:01.000",
            NextDelta = "00:00.250",
            PreviousPosition = 8,
            Position = 5,
            LevelDecayedPoints = 123.6
        });

        Assert.Equal(
            $"<size=75%>PB improved by {TextColour.Pink.Wrap("00:01.000")}</size><br>" +
            $"<size=75%>#8 -> #5 <size=60%>({TextColour.Yellow.Wrap("124pts")})</size></size><br>" +
            $"<size=50%>Gap to next player: {TextColour.Pink.Wrap("00:00.250")}</size>",
            result);
    }

    [Fact]
    public void FormatsCompactWorldRecordFeedbackAndHighlightsFirstPlace()
    {
        string result = RecordFeedbackFormatter.Format(new RecordFeedbackMessageData
        {
            Kind = RecordFeedbackKind.ImprovedWorldRecord,
            PreviousDelta = "00:00.500",
            PreviousPosition = 2,
            Position = 1,
            LevelDecayedPoints = 100
        });

        Assert.Equal(
            $"<size=75%>WR improved by {TextColour.Pink.Wrap("00:00.500")}</size><br>" +
            $"<size=75%>#2 -> {TextColour.Yellow.Wrap("#1")} " +
            $"<size=60%>({TextColour.Yellow.Wrap("100pts")})</size></size>",
            result);
    }

    [Fact]
    public void FirstPersonalBestUsesOnlyEntryAndGapLines()
    {
        string result = RecordFeedbackFormatter.Format(new RecordFeedbackMessageData
        {
            Kind = RecordFeedbackKind.FirstPersonalBest,
            NextDelta = "00:02.000",
            WasFirstPersonalBest = true,
            Position = 42,
            LevelDecayedPoints = 10
        });

        Assert.Equal(
            $"<size=75%>#42 <size=60%>({TextColour.Yellow.Wrap("10pts")})</size></size><br>" +
            $"<size=50%>Gap to next player: {TextColour.Pink.Wrap("00:02.000")}</size>",
            result);
    }

    [Fact]
    public void FirstPersonalBestWorldRecordUsesOnlyHighlightedEntryLine()
    {
        string result = RecordFeedbackFormatter.Format(new RecordFeedbackMessageData
        {
            Kind = RecordFeedbackKind.NewWorldRecord,
            WasFirstPersonalBest = true,
            Position = 1,
            LevelDecayedPoints = 100
        });

        Assert.Equal(
            $"<size=75%>{TextColour.Yellow.Wrap("#1")} " +
            $"<size=60%>({TextColour.Yellow.Wrap("100pts")})</size></size>",
            result);
    }

    [Fact]
    public void RemovedHeadlinesProduceNoFallbackText()
    {
        Assert.Equal(string.Empty, RecordFeedbackFormatter.Format(new RecordFeedbackMessageData
        {
            Kind = RecordFeedbackKind.NewWorldRecord
        }));
    }

    [Fact]
    public void ConfigPolicySeparatesMessageKinds()
    {
        Assert.False(RecordFeedbackFormatter.ShouldShow(RecordFeedbackKind.PersonalBest, false, true, true));
        Assert.False(RecordFeedbackFormatter.ShouldShow(RecordFeedbackKind.NewWorldRecord, true, false, true));
        Assert.False(RecordFeedbackFormatter.ShouldShow(RecordFeedbackKind.ImprovedWorldRecord, true, true, false));
    }

    [Theory]
    [InlineData(RecordFeedbackKind.PersonalBest, true, false, false)]
    [InlineData(RecordFeedbackKind.FirstPersonalBest, false, false, false)]
    [InlineData(RecordFeedbackKind.NewWorldRecord, true, true, false)]
    [InlineData(RecordFeedbackKind.ImprovedWorldRecord, true, true, true)]
    public void ClassifiesConfirmedSubscriptionResult(
        RecordFeedbackKind expected,
        bool hadPreviousPersonalBest,
        bool isWorldRecord,
        bool previousWorldRecordOwnedByPlayer)
    {
        Assert.Equal(expected, RecordFeedbackFormatter.ClassifyConfirmed(
            hadPreviousPersonalBest,
            isWorldRecord,
            previousWorldRecordOwnedByPlayer));
    }

    [Theory]
    [InlineData(RecordFeedbackKind.PersonalBest, true)]
    [InlineData(RecordFeedbackKind.FirstPersonalBest, true)]
    [InlineData(RecordFeedbackKind.NewWorldRecord, false)]
    [InlineData(RecordFeedbackKind.ImprovedWorldRecord, false)]
    [InlineData(RecordFeedbackKind.None, false)]
    public void OnlyNonWorldRecordPersonalBestsRequestNextFastest(
        RecordFeedbackKind kind,
        bool expected)
    {
        Assert.Equal(expected, RecordFeedbackFormatter.RequiresNextFastest(kind));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(null, false)]
    public void DetectsWorldRecordFromLevelPosition(int? levelPosition, bool expected)
    {
        Assert.Equal(expected, RecordFeedbackFormatter.IsWorldRecordPosition(levelPosition));
    }

    [Theory]
    [InlineData(true, 5, 100.0, true)]
    [InlineData(false, 5, 100.0, false)]
    [InlineData(true, null, 100.0, false)]
    [InlineData(true, 5, null, false)]
    public void RequiresMatchingRankedProjection(
        bool matchingPersonalBest,
        int? position,
        double? levelPoints,
        bool expected)
    {
        Assert.Equal(expected,
            RecordFeedbackFormatter.HasRankedProjection(matchingPersonalBest, position, levelPoints));
    }
}
