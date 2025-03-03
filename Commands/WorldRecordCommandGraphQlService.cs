using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.GTR.Api;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.Commands;

public class WorldRecordCommandGraphQlService
{
    private const string Query
        = "fragment frag on Record{time userByIdUser{steamName}}query personalbests($hash:String){allWorldRecordGlobals(filter:{levelByIdLevel:{hash:{equalTo:$hash}}}){nodes{recordByIdRecord{...frag}}}}";

    private readonly GraphQLApiHttpClient _client;

    public WorldRecordCommandGraphQlService(GraphQLApiHttpClient client)
    {
        _client = client;
    }

    public async UniTask<Result<WorldRecords>> GetWorldRecord(string levelHash)
    {
        Result<Root> result = await _client.PostAsync<Root>(
            Query,
            new
            {
                hash = levelHash
            });

        if (result.IsFailed)
        {
            return result.ToResult();
        }

        return Result.Ok(MapToWorldRecords(result.Value));
    }

    private static WorldRecords MapToWorldRecords(Root root)
    {
        return new WorldRecords
        {
            Global = MapToWorldRecord(root.Data.AllWorldRecordGlobals)
        };
    }

    private static WorldRecord MapToWorldRecord(RecordCollection recordCollection)
    {
        Node recordNode = recordCollection.Nodes.FirstOrDefault();
        if (recordNode?.RecordByIdRecord == null)
            return null;

        return new WorldRecord
        {
            Time = recordNode.RecordByIdRecord.Time,
            SteamName = recordNode.RecordByIdRecord.UserByIdUser.SteamName
        };
    }

    private class Root
    {
        public Data Data { get; set; }
    }

    private class Data
    {
        public RecordCollection AllWorldRecordGlobals { get; set; }
    }

    private class RecordCollection
    {
        public List<Node> Nodes { get; set; }
    }

    private class Node
    {
        public Record RecordByIdRecord { get; set; }
    }

    private class Record
    {
        public double Time { get; set; }
        public User UserByIdUser { get; set; }
    }

    private class User
    {
        public string SteamName { get; set; }
    }
}
