using TNRD.Zeepkist.GTR.UI;
using TNRD.Zeepkist.GTR.UI.Medals;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class RecordHolderPresentationStateTests
{
    private static PersonalBestHolder PersonalBest(double time = 50, int? medal = 2, int? rank = 10) =>
        new() { Time = time, Medal = medal, Rank = rank };

    [Theory]
    [InlineData(49, 2, 10)]
    [InlineData(50, 3, 10)]
    [InlineData(50, 2, 9)]
    [InlineData(50, 2, 11)]
    [InlineData(50, 2, null)]
    public void TimeMedalOrRankChangeRequestsPersonalBestView(double time, int? medal, int? rank)
    {
        var state = new RecordHolderPresentationState();
        state.Observe("level", PersonalBest());
        Assert.True(state.Observe("level", PersonalBest(time, medal, rank)).PersonalBestChanged);
    }

    [Fact]
    public void DuplicateOrPointsOnlySnapshotDoesNotResetPresentation()
    {
        var state = new RecordHolderPresentationState();
        var personalBest = PersonalBest();
        state.Observe("level", personalBest);
        state.ResetTimer(5);
        state.Tick(2);
        personalBest.LevelDecayedPoints = 999;
        personalBest.PlayerDecayedPoints = 1000;
        Assert.False(state.Observe("level", personalBest).PersonalBestChanged);
        Assert.Equal(3, state.RemainingTime);
        Assert.False(state.Observe("level", PersonalBest()).PersonalBestChanged);
    }

    [Fact]
    public void LateMedalProjectionAnimatesEvenWhenTimeIsUnchanged()
    {
        var state = new RecordHolderPresentationState();
        state.Observe("level", PersonalBest(medal: 1));
        var update = state.Observe("level", PersonalBest(medal: 5));
        Assert.True(update.PersonalBestChanged);
        Assert.Equal(2, update.MedalUpgrade.First);
        Assert.Equal(5, update.MedalUpgrade.Last);
        Assert.Null(state.Observe("level", PersonalBest(medal: 5)).MedalUpgrade);
    }

    [Fact]
    public void ReusesSubscriptionUpgradeSoRepeatedSnapshotCannotReplayIt()
    {
        var state = new RecordHolderPresentationState();
        state.Observe("level", PersonalBest());
        var personalBest = PersonalBest(40, 4, 5);
        personalBest.MedalUpgrade = new MedalUpgrade(3, 4);
        var update = state.Observe("level", personalBest);
        Assert.Same(personalBest.MedalUpgrade, update.MedalUpgrade);
        Assert.False(state.Observe("level", personalBest).PersonalBestChanged);
    }

    [Fact]
    public void InitialSnapshotSelectsPersonalBestWithoutCelebratingExistingMedal()
    {
        var update = new RecordHolderPresentationState().Observe("level", PersonalBest());
        Assert.True(update.PersonalBestChanged);
        Assert.Null(update.MedalUpgrade);
    }

    [Fact]
    public void FirstMedalAfterKnownEmptyPersonalBestAnimates()
    {
        var state = new RecordHolderPresentationState();
        Assert.False(state.Observe("level", null).PersonalBestChanged);
        var update = state.Observe("level", PersonalBest());
        Assert.True(update.PersonalBestChanged);
        Assert.Equal(0, update.MedalUpgrade.First);
        Assert.Equal(2, update.MedalUpgrade.Last);
    }

    [Fact]
    public void RankOrTimeOnlyChangeDoesNotInventMedalAnimation()
    {
        var state = new RecordHolderPresentationState();
        state.Observe("level", PersonalBest());
        Assert.Null(state.Observe("level", PersonalBest(rank: 9)).MedalUpgrade);
        Assert.Null(state.Observe("level", PersonalBest(49, 2, 9)).MedalUpgrade);
        Assert.Null(state.Observe("level", PersonalBest(49, 1, 9)).MedalUpgrade);
    }

    [Fact]
    public void EachViewSwitchGetsFullDisplayInterval()
    {
        var state = new RecordHolderPresentationState();
        state.ResetTimer(5);
        Assert.False(state.Tick(4));
        state.ResetTimer(5);
        Assert.False(state.Tick(4));
        Assert.True(state.Tick(1));
        state.ResetTimer(5);
        Assert.Equal(5, state.RemainingTime);
    }

    [Fact]
    public void LevelChangesAndReconnectsDoNotAnimateOldMedals()
    {
        var state = new RecordHolderPresentationState();
        state.Observe("level", PersonalBest());
        Assert.Null(state.Observe("other", PersonalBest(30, 5)).MedalUpgrade);
        state.Reset();
        Assert.Null(state.Observe("other", PersonalBest(25, 5)).MedalUpgrade);
    }
}
