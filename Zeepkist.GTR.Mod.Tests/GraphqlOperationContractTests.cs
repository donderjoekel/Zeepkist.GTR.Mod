using System.Text.RegularExpressions;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class GraphqlOperationContractTests
{
    [Fact]
    public void TournamentOperationsUseLevelFallbackAndRankedPagination()
    {
        string operation = ReadOperation("TrackTournamentLeaderboards.graphql");

        Assert.Contains("first: 2", operation);
        Assert.Contains("startAt: { lessThanOrEqualTo: $now }", operation);
        Assert.Contains("{ xxHash: { equalTo: $xxHash } }", operation);
        Assert.Contains("{ hash: { equalTo: $hash } }", operation);
        Assert.Contains("{ adventure: { equalTo: true } }", operation);
        Assert.Contains("orderBy: [TYPE_ASC, START_AT_DESC, ID_DESC]", operation);
        Assert.Contains("orderBy: [RANK_ASC, TIME_ASC, RECORD_ID_ASC]", operation);
        Assert.Contains("first: $first", operation);
        Assert.Contains("offset: $offset", operation);
        Assert.Contains("rank", operation);
        Assert.Contains("points", operation);
    }

    [Fact]
    public void CurrentLevelPersonalBestUsesHistoryProjection()
    {
        string operation = ReadOperation("CurrentLevelRecords.graphql");
        string personalBest = Regex.Match(
            operation,
            @"personalBest: recordHistoryEntries\([\s\S]*?\)\s*\{").Value;

        Assert.Contains("historyView: \"personal-bests\"", personalBest);
        Assert.Contains("levelId: $levelId", personalBest);
        Assert.Contains("userId: $userId", personalBest);
        Assert.Contains("orderBy: [ID_DESC]", personalBest);
        Assert.DoesNotContain("levelXxHash", operation);
        Assert.DoesNotContain("userSteamId: { equalTo:", operation);
        Assert.Contains("levelPosition", operation);
        Assert.Contains("levelDecayedPoints", operation);
    }

    [Fact]
    public void CurrentLevelIdResolverUsesAdventureFallbackAndSteamIdentity()
    {
        string operation = ReadOperation("ResolveCurrentLevelRecordIds.graphql");

        Assert.Contains("{ xxHash: { equalTo: $xxHash } }", operation);
        Assert.Contains("{ hash: { equalTo: $hash } }", operation);
        Assert.Contains("{ adventure: { equalTo: true } }", operation);
        Assert.Contains("orderBy: [ID_DESC]", operation);
        Assert.Contains("userBySteamId(steamId: $steamId)", operation);
    }

    private static string ReadOperation(string name)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        return File.ReadAllText(Path.Combine(root, "GraphQL", "Queries", name));
    }
}
