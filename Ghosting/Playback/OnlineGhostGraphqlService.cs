using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JetBrains.Annotations;
using Steamworks;
using StrawberryShake;
using TNRD.Zeepkist.GTR.Configuration;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using Result = ZeepSDK.External.FluentResults.Result;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class OnlineGhostGraphqlService
{
    private readonly ConfigService _configService;
    private readonly IGtrClient _gtrClient;

    public IGtrClient GtrClient => _gtrClient;

    public OnlineGhostGraphqlService(ConfigService configService, IGtrClient gtrClient)
    {
        _configService = configService;
        _gtrClient = gtrClient;
    }

    public async UniTask<Result<IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes>>> GetPersonalBests(
        string levelHash)
    {
        IOperationResult<IGetPersonalBestGhostsResult> result =
            await _gtrClient.GetPersonalBestGhosts.ExecuteAsync(SteamClient.SteamId.ToString(), levelHash);

        try
        {
            result.EnsureNoErrors();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes> nodes = result.Data.PersonalBestGlobals.Nodes;
        return Result.Ok(nodes);
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
