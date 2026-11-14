using TNRD.Zeepkist.GTR.Leaderboard;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class RoundMedalStateTests
{
    [Theory]
    [InlineData(RoundMedalState.OfficialRoundTab, false, true)]
    [InlineData(RoundMedalState.OfficialChampionshipTab, true, true)]
    [InlineData(RoundMedalState.OfficialRoundTab, true, false)]
    [InlineData(RoundMedalState.OfficialChampionshipTab, false, false)]
    [InlineData("TNRD.Zeepkist.GTR.Leaderboard.OnlineLeaderboardTab", false, false)]
    [InlineData("TNRD.Zeepkist.GTR.Leaderboard.OnlineTrackTournamentLeaderboardTab", true, false)]
    [InlineData(null, false, false)]
    public void MedalsBelongOnlyToMatchingOfficialTab(string typeName, bool championship, bool expected) =>
        Assert.Equal(expected, RoundMedalState.IsOfficialTab(typeName, championship));

    [Fact]
    public void SwitchingOfficialTabsReusesTargetsWithoutReplayingUpgrade()
    {
        var state = Ready();
        var targets = state.Targets;
        int generation = state.Generation;
        state.ObserveLocal(0, true, 60, true);
        state.ResetPresentation();
        Assert.False(state.BeginLevel("level"));
        Assert.Same(targets, state.Targets);
        Assert.Equal(generation, state.Generation);
        Assert.Null(state.ObserveLocal(0, true, 40, true));
        Assert.NotNull(state.ObserveLocal(0, true, 35, true));
    }

    private static RoundMedalState Ready()
    {
        var state = new RoundMedalState();
        state.BeginLevel("level");
        state.SetTargets(state.Generation, LevelMedalTargetsTests.Targets());
        return state;
    }

    [Fact]
    public void QueriesOncePerLoadedLevelIncludingMissingMetadata()
    {
        var state = new RoundMedalState();
        Assert.False(state.BeginLevel(null));
        Assert.True(state.BeginLevel("level"));
        int generation = state.Generation;
        Assert.False(state.BeginLevel("level"));
        Assert.True(state.SetTargets(generation, null));
        Assert.False(state.BeginLevel("level"));
        Assert.Equal(generation, state.Generation);
        state.ResetLevel();
        Assert.True(state.BeginLevel("level"));
    }

    [Fact]
    public void StaleResponseCannotReplaceCurrentTargets()
    {
        var state = Ready();
        int oldGeneration = state.Generation;
        state.BeginLevel("other");
        Assert.False(state.SetTargets(oldGeneration, LevelMedalTargetsTests.Targets()));
        Assert.Null(state.Targets);
        Assert.Null(state.ObserveLocal(0, true, 30, true));
        Assert.True(state.SetTargets(state.Generation, LevelMedalTargetsTests.Targets()));
        Assert.Null(state.ObserveLocal(0, true, 30, true));
    }

    [Fact]
    public void CompletedLobbyResultAnimatesWithoutDatabasePersonalBest()
    {
        var state = Ready();
        Assert.Null(state.ObserveLocal(0, true, null, false));
        var upgrade = state.ObserveLocal(0, true, 30, true);
        Assert.Equal(0, upgrade.First);
        Assert.Equal(5, upgrade.Last);
        Assert.Null(state.ObserveLocal(0, true, 30, true));
    }

    [Fact]
    public void IncompleteFasterTimeDoesNotEarnMedal()
    {
        var state = Ready();
        Assert.Null(state.ObserveLocal(0, true, 60, true));
        Assert.Null(state.ObserveLocal(0, true, 30, false));
    }

    [Fact]
    public void VisibleUpgradeVisitsEveryNewTier()
    {
        var state = Ready();
        Assert.Null(state.ObserveLocal(0, true, 55, true));
        var upgrade = state.ObserveLocal(0, true, 35, true);
        Assert.Equal(2, upgrade.First);
        Assert.Equal(5, upgrade.Last);
    }

    [Fact]
    public void PageChangesAndHiddenRowsDoNotReplayUpgrades()
    {
        var state = Ready();
        state.ObserveLocal(0, true, 60, true);
        Assert.Null(state.ObserveLocal(1, true, 50, true));
        Assert.Null(state.ObserveLocal(1, false, null, false));
        Assert.Null(state.ObserveLocal(0, true, 40, true));
        Assert.NotNull(state.ObserveLocal(0, true, 35, true));
    }

    [Fact]
    public void ClosingTabReconnectAndNewTargetsResetBaselines()
    {
        var state = Ready();
        state.ObserveLocal(0, true, 60, true);
        state.ResetPresentation();
        Assert.Null(state.ObserveLocal(0, true, 50, true));
        state.SetTargets(state.Generation, LevelMedalTargetsTests.Targets());
        Assert.Null(state.ObserveLocal(0, true, 40, true));
        state.ResetLevel();
        state.BeginLevel("level");
        state.SetTargets(state.Generation, LevelMedalTargetsTests.Targets());
        Assert.Null(state.ObserveLocal(0, true, 35, true));
    }
}
