using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using TNRD.Zeepkist.GTR.FluentResults;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Mod.ChatCommands;

public class RankLocalChatCommand : ILocalChatCommand
{
    private static readonly ManualLogSource logger = LoggerFactory.GetLogger<RankLocalChatCommand>();

    public string Prefix => "/";
    public string Command => "rank";
    public string Description => "Shows your current rank, points, and # of world records";

    public void Handle(string arguments)
    {
        GetCurrentPlayerRank(SdkWrapper.Instance.UsersApi.UserId).Forget();
    }

    private async UniTaskVoid GetCurrentPlayerRank(int playerId)
    {
        Result<Root> result = await SdkWrapper.Instance.GraphQLClient.Post<Root>(
            $$"""
              {
                  "query": "{ userById(id: {{playerId}}) { playerPointsByUser { nodes { points rank } } } allWorldRecords(condition: {user: {{playerId}}}) { totalCount } }"
              }
              """);

        if (result.IsFailed)
        {
            ChatApi.AddLocalMessage("[GTR] Failed to get player rank");
            logger.LogError("[GTR] Failed to get player rank");
            logger.LogInfo(result.ToString());
            return;
        }

        if (result.Value.data.userById.playerPointsByUser.nodes.Count == 0)
        {
            ChatApi.SendMessage("No rank set yet");
            return;
        }

        Root.UserById.PlayerPointsByUser.Node node = result.Value.data.userById.playerPointsByUser.nodes.First();

        string message = "Rank: " + node.rank + "<br>" +
                         "Points: " + node.points + "<br>" +
                         "World Records: " + result.Value.data.allWorldRecords.totalCount;

        ChatApi.SendMessage(message);
    }

    public class Root
    {
        public Data data { get; set; }

        public class Data
        {
            public UserById userById { get; set; }
            public AllWorldRecords allWorldRecords { get; set; }
        }

        public class UserById
        {
            public PlayerPointsByUser playerPointsByUser { get; set; }

            public class PlayerPointsByUser
            {
                public List<Node> nodes { get; set; }

                public class Node
                {
                    public int points { get; set; }
                    public int rank { get; set; }
                }
            }
        }

        public class AllWorldRecords
        {
            public int totalCount { get; set; }
        }
    }
}
