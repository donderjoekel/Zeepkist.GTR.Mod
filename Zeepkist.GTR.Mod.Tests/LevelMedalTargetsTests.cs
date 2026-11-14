using TNRD.Zeepkist.GTR.UI.Medals;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class LevelMedalTargetsTests
{
    internal static LevelMedalTargets Targets() => new()
    {
        Bronze = 60, Silver = 55, Gold = 50, Author = 45, Expert = 40, Apex = 35
    };

    [Theory]
    [InlineData(65, null)]
    [InlineData(60, 0)]
    [InlineData(57, 0)]
    [InlineData(55, 1)]
    [InlineData(50, 2)]
    [InlineData(45, 3)]
    [InlineData(40, 4)]
    [InlineData(35, 5)]
    [InlineData(30, 5)]
    public void HighestTierIncludesEqualTargets(double time, int? expected) =>
        Assert.Equal(expected, Targets().Classify(time));

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(1e12)]
    [InlineData(0.0001)]
    public void InvalidOrZeroMillisecondResultEarnsNothing(double? time) =>
        Assert.Null(Targets().Classify(time));

    [Fact]
    public void IncompleteResultNeverEarnsMedal() => Assert.Null(Targets().Classify(30, completed: false));

    [Fact]
    public void MissingCompetitiveTargetsStillAllowBaseMedals()
    {
        var targets = Targets();
        targets.Expert = null;
        targets.Apex = null;
        Assert.Equal(3, targets.Classify(30));
        Assert.Null(new LevelMedalTargets().Classify(30));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1e12)]
    public void InvalidTargetsAreSkipped(double invalid)
    {
        var targets = Targets();
        targets.Apex = invalid;
        targets.Expert = invalid;
        Assert.Equal(3, targets.Classify(30));
    }

    [Theory]
    [InlineData(40.00049, 4)]
    [InlineData(40.00051, 3)]
    [InlineData(39.99951, 4)]
    public void UsesServerMillisecondRounding(double time, int tier) =>
        Assert.Equal(tier, Targets().Classify(time));

    [Fact]
    public void TargetsAlsoRoundToNearestMillisecond()
    {
        var targets = new LevelMedalTargets { Apex = 40.0006 };
        Assert.Equal(5, targets.Classify(40.001));
        Assert.Null(targets.Classify(40.002));
        Assert.Equal(1235, LevelMedalTargets.Milliseconds(1.2345));
    }
}
