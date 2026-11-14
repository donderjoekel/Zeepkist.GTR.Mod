using TNRD.Zeepkist.GTR.UI.Medals;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class MedalUpgradeTrackerTests
{
    [Theory]
    [InlineData(0, MedalTier.Bronze)]
    [InlineData(1, MedalTier.Silver)]
    [InlineData(2, MedalTier.Gold)]
    [InlineData(3, MedalTier.Author)]
    [InlineData(4, MedalTier.Expert)]
    [InlineData(5, MedalTier.Apex)]
    public void TierMatchesServer(int value, MedalTier tier)
    {
        Assert.Equal((int)tier, MedalUpgradeTracker.Normalize(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(int.MaxValue)]
    public void MissingOrUnknownTierHasNoIcon(int? value)
    {
        Assert.Null(MedalUpgradeTracker.Normalize(value));
    }

    [Fact]
    public void InitialSnapshotNeverAnimates()
    {
        Assert.Null(new MedalUpgradeTracker().Update("level", 30, 5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(80.0)]
    public void FirstMedalFromKnownMedalFreeStateStartsWithBronze(double? oldTime)
    {
        var tracker = new MedalUpgradeTracker();
        tracker.Update("level", oldTime, null);
        MedalUpgrade upgrade = tracker.Update("level", 60, 2);
        Assert.Equal(0, upgrade.First);
        Assert.Equal(2, upgrade.Last);
    }

    [Fact]
    public void MultiTierUpgradeVisitsOnlyNewMedals()
    {
        var tracker = new MedalUpgradeTracker();
        tracker.Update("level", 60, 1);
        MedalUpgrade upgrade = tracker.Update("level", 30, 5);
        Assert.Equal(new[] { 2, 3, 4, 5 }, Enumerable.Range(upgrade.First, upgrade.Last - upgrade.First + 1));
        Assert.Null(tracker.Update("level", 30, 5));
    }

    [Fact]
    public void ThresholdOnlyChangeDoesNotCelebrateOrReplay()
    {
        var tracker = new MedalUpgradeTracker();
        tracker.Update("level", 40, 3);
        Assert.Null(tracker.Update("level", 40, 4));
        Assert.Null(tracker.Update("level", 39, 4));
        Assert.NotNull(tracker.Update("level", 30, 5));
    }

    [Fact]
    public void ReconnectAndLevelChangeEstablishFreshBaselines()
    {
        var tracker = new MedalUpgradeTracker();
        tracker.Update("level", 60, 0);
        Assert.Null(tracker.Update("level", 40, 4, baseline: true));
        Assert.NotNull(tracker.Update("level", 30, 5));
        Assert.Null(tracker.Update("other", 25, 5));
        tracker.Reset();
        Assert.Null(tracker.Update("other", 20, 5));
    }

    [Theory]
    [InlineData(40, 2)]
    [InlineData(60, 4)]
    [InlineData(40, 3)]
    [InlineData(40, null)]
    [InlineData(40, 99)]
    [InlineData(0, 5)]
    [InlineData(double.NaN, 5)]
    [InlineData(double.PositiveInfinity, 5)]
    public void NonUpgradeDoesNotAnimate(double time, int? medal)
    {
        var tracker = new MedalUpgradeTracker();
        tracker.Update("level", 50, 3);
        Assert.Null(tracker.Update("level", time, medal));
    }
}
