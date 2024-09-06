using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TNRD.Zeepkist.GTR.Api;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class LeaderboardGraphqlService
{
    private const string Query
        = "query personalbests($hash:String){allPersonalBestGlobals(filter:{levelByIdLevel:{hash:{equalTo:$hash}}}){nodes{userByIdUser{steamName steamId}recordByIdRecord{time}}}allLevelPoints(filter:{levelByIdLevel:{hash:{equalTo:$hash}}}){nodes{points}}allUsers{totalCount}}";

    private readonly GraphQLApiHttpClient _client;

    public LeaderboardGraphqlService(GraphQLApiHttpClient client)
    {
        _client = client;
    }

    public async UniTask<Result<LeaderboardRecords>> GetLeaderboardRecords(string levelHash,
        CancellationToken ct = default)
    {
        Result<Root> result = await _client.PostAsync<Root>(Query, new { hash = levelHash }, ct);
        if (ct.IsCancellationRequested)
            return Result.Ok();

        if (result.IsFailed)
        {
            return result.ToResult();
        }

        return Result.Ok(Map(result.Value.Data));
    }

    private static LeaderboardRecords Map(Data data)
    {
        return new LeaderboardRecords
        {
            Records = Map(data.AllPersonalBestGlobals),
            LevelPoints = Map(data.AllLevelPoints),
            TotalUsers = Map(data.AllUsers)
        };
    }

    private static int Map(AllUsers allUsers)
    {
        return allUsers.TotalCount;
    }

    private static double Map(AllLevelPoints allLevelPoints)
    {
        return allLevelPoints.Nodes.FirstOrDefault()?.Points ?? 0;
    }

    private static List<LeaderboardRecord> Map(AllPersonalBestGlobals globals)
    {
        return globals.Nodes.Select(Map).ToList();
    }

    private static LeaderboardRecord Map(Node node)
    {
        return new LeaderboardRecord
        {
            SteamName = node.UserByIdUser.SteamName,
            SteamId = node.UserByIdUser.SteamId,
            Time = node.RecordByIdRecord.Time
        };
    }

    private class Root
    {
        public Data Data { get; set; }
    }

    private class Data
    {
        public AllPersonalBestGlobals AllPersonalBestGlobals { get; set; }
        public AllLevelPoints AllLevelPoints { get; set; }
        public AllUsers AllUsers { get; set; }
    }

    private class AllPersonalBestGlobals
    {
        public List<Node> Nodes { get; set; }
    }

    private class Node
    {
        public UserByIdUser UserByIdUser { get; set; }
        public RecordByIdRecord RecordByIdRecord { get; set; }
    }

    private class UserByIdUser
    {
        public string SteamName { get; set; }
        public string SteamId { get; set; }
    }

    private class RecordByIdRecord
    {
        public double Time { get; set; }
    }

    private class AllLevelPoints
    {
        public List<LevelPointNode> Nodes { get; set; }
    }

    private class LevelPointNode
    {
        public int Points { get; set; }
    }

    private class AllUsers
    {
        public int TotalCount { get; set; }
    }
}
