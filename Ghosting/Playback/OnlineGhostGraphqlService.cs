using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JetBrains.Annotations;
using Steamworks;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Configuration;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using Result = ZeepSDK.External.FluentResults.Result;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class OnlineGhostGraphqlService
{
    private const string Query
        = "fragment frag on Record{id userByIdUser{steamName}recordMediasByIdRecord{nodes{ghostUrl}}}query personalbests($steamId:BigFloat$hash:String){allPersonalBestGlobals(filter:{levelByIdLevel:{hash:{equalTo:$hash}}userByIdUser:{steamId:{equalTo:$steamId}}}){nodes{recordByIdRecord{...frag}}}}";

    private readonly GraphQLApiHttpClient _client;
    private readonly ConfigService _configService;

    protected GraphQLApiHttpClient Client => _client;

    public OnlineGhostGraphqlService(GraphQLApiHttpClient client, ConfigService configService)
    {
        _client = client;
        _configService = configService;
    }

    public async UniTask<Result<List<PersonalBest>>> GetPersonalBests(string levelHash)
    {
        Result<Root> result = await _client.PostAsync<Root>(
            Query,
            new
            {
                steamId = SteamClient.SteamId.ToString(),
                hash = levelHash
            });


        if (result.IsFailed)
        {
            return result.ToResult();
        }

        return Result.Ok(GetUniquePersonalBests(Map(result.Value)));
    }

    private List<PersonalBest> GetUniquePersonalBests(PersonalBests personalBests)
    {
        List<PersonalBest> uniquePersonalBests = [];

        if (personalBests.Global != null && _configService.ShowGlobalPersonalBest.Value)
        {
            uniquePersonalBests.Add(personalBests.Global with { Type = GhostType.Global });
        }

        return uniquePersonalBests;
    }

    private class PersonalBests
    {
        public PersonalBest Global { get; set; }
    }

    public record PersonalBest
    {
        public int Id { get; set; }
        public string SteamName { get; set; }
        public string GhostUrl { get; set; }
        public GhostType Type { get; set; }
    }

    private static PersonalBests Map(Root root)
    {
        return Map(root.Data);
    }

    private static PersonalBests Map(Data data)
    {
        return new PersonalBests
        {
            Global = Map(data.AllPersonalBestGlobals)
        };
    }

    private static PersonalBest Map(AllPersonalBestGlobals globals)
    {
        return Map(globals.Nodes.FirstOrDefault()?.RecordByIdRecord);
    }

    protected static PersonalBest Map(RecordByIdRecord record)
    {
        if (record == null)
            return null;

        return new PersonalBest
        {
            Id = record.Id,
            SteamName = Map(record.UserByIdUser),
            GhostUrl = Map(record.RecordMediasByIdRecord)
        };
    }

    protected static string Map(UserByIdUser userByIdUser)
    {
        return userByIdUser.SteamName;
    }

    protected static string Map(RecordMediasByIdRecord recordMediasByIdRecord)
    {
        return Map(recordMediasByIdRecord.Nodes.FirstOrDefault());
    }

    protected static string Map(RecordMediaNode recordMediaNode)
    {
        return recordMediaNode == null ? string.Empty : recordMediaNode.GhostUrl;
    }

    [UsedImplicitly]
    private class Root
    {
        public Data Data { get; set; }
    }

    [UsedImplicitly]
    private class Data
    {
        public AllPersonalBestGlobals AllPersonalBestGlobals { get; set; }
    }

    [UsedImplicitly]
    protected class AllPersonalBestGlobals
    {
        [UsedImplicitly] public List<Node> Nodes { get; set; }
    }

    [UsedImplicitly]
    protected class Node
    {
        public RecordByIdRecord RecordByIdRecord { get; set; }
    }

    [UsedImplicitly]
    protected class RecordByIdRecord
    {
        public int Id { get; set; }
        public UserByIdUser UserByIdUser { get; set; }
        public RecordMediasByIdRecord RecordMediasByIdRecord { get; set; }
    }

    [UsedImplicitly]
    protected class UserByIdUser
    {
        public string SteamName { get; set; }
    }

    [UsedImplicitly]
    protected class RecordMediasByIdRecord
    {
        [UsedImplicitly] public List<RecordMediaNode> Nodes { get; set; }
    }

    [UsedImplicitly]
    protected class RecordMediaNode
    {
        public string GhostUrl { get; set; }
    }
}
