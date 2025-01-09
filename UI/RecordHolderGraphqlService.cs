using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Api;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.UI;

public class RecordHolderGraphqlService
{
    private const string Query
        = """
          query GetRecordHolders($steamId: BigFloat, $hash: String) {
            allPersonalBestGlobals(
              filter: {
                levelByIdLevel: { hash: { equalTo: $hash } }
                userByIdUser: { steamId: { equalTo: $steamId } }
              }
            ) {
              nodes {
                recordByIdRecord {
                  time
                  userByIdUser {
                    steamName
                  }
                }
              }
            }
            allWorldRecordGlobals(
              filter: { levelByIdLevel: { hash: { equalTo: $hash } } }
            ) {
              nodes {
                recordByIdRecord {
                  time
                  userByIdUser {
                    steamName
                  }
                }
              }
            }
          }
          """;

    private readonly GraphQLApiHttpClient _client;
    private readonly ILogger<RecordHolderGraphqlService> _logger;

    public RecordHolderGraphqlService(GraphQLApiHttpClient client, ILogger<RecordHolderGraphqlService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async UniTask<Result<RecordHolders>> GetRecordHolders(string levelHash, ulong steamId)
    {
        _logger.LogInformation(
            "Getting record holders for level {LevelHash} and steam id {SteamId}",
            levelHash,
            steamId);

        Result<Root> result = await _client.PostAsync<Root>(
            Query,
            new
            {
                steamId = steamId.ToString(),
                hash = levelHash
            });

        if (result.IsFailed)
        {
            return result.ToResult();
        }

        try
        {
            return Result.Ok(MapToRecordHolders(result.Value));
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }
    }

    private static RecordHolders MapToRecordHolders(Root root)
    {
        Node worldRecordNode = root.Data.AllWorldRecordGlobals.Nodes.FirstOrDefault();
        Node personalBestNode = root.Data.AllPersonalBestGlobals.Nodes.FirstOrDefault();

        return new RecordHolders
        {
            WorldRecord = new WorldRecordHolder
            {
                Time = worldRecordNode?.RecordByIdRecord.Time ?? -1,
                SteamName = worldRecordNode?.RecordByIdRecord.UserByIdUser.SteamName
            },
            PersonalBest = new PersonalBestHolder
            {
                Time = personalBestNode?.RecordByIdRecord.Time ?? -1
            }
        };
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
        public AllWorldRecordGlobals AllWorldRecordGlobals { get; set; }
    }

    [UsedImplicitly]
    private class AllPersonalBestGlobals
    {
        public List<Node> Nodes { get; set; }
    }

    [UsedImplicitly]
    private class AllWorldRecordGlobals
    {
        public List<Node> Nodes { get; set; }
    }

    [UsedImplicitly]
    private class Node
    {
        public RecordByIdRecord RecordByIdRecord { get; set; }
    }

    [UsedImplicitly]
    private class RecordByIdRecord
    {
        public double Time { get; set; }
        public UserByIdUser UserByIdUser { get; set; }
    }

    [UsedImplicitly]
    private class UserByIdUser
    {
        public string SteamId { get; set; }
        public string SteamName { get; set; }
    }
}
