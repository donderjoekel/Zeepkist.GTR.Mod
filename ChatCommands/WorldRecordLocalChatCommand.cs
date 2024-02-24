using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.FluentResults;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;
using ZeepSDK.Level;
using ZeepSDK.Multiplayer;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Mod.ChatCommands;

public class WorldRecordLocalChatCommand : ILocalChatCommand
{
    private static readonly ManualLogSource logger = LoggerFactory.GetLogger<WorldRecordLocalChatCommand>();

    public string Prefix => "/";
    public string Command => "wr";
    public string Description => "Shows the world record for the current level";

    public void Handle(string arguments)
    {
        LevelScriptableObject levelScriptableObject = MultiplayerApi.GetCurrentLevel();
        if (levelScriptableObject == null)
        {
            ChatApi.AddLocalMessage("Unable to get the current level, try again later");
        }
        else
        {
            GetCurrentWorldRecord(LevelApi.GetLevelHash(levelScriptableObject)).Forget();
        }
    }

    private async UniTaskVoid GetCurrentWorldRecord(string levelHash)
    {
        Result<Root> result = await SdkWrapper.Instance.GraphQLClient.Post<Root>(
            $$"""
              {
                  "query": "{ allWorldRecords(condition: {level: \"{{levelHash}}\"}) { nodes { recordByRecord { time } userByUser { steamName } } } }"
              }
              """);

        if (result.IsFailed)
        {
            ChatApi.AddLocalMessage("[GTR] Failed to get world record");
            logger.LogError("[GTR] Failed to get world record");
            logger.LogInfo(result.ToString());
            return;
        }

        if (result.Value.data.allWorldRecords.nodes.Count == 0)
        {
            ChatApi.SendMessage("No world record set yet");
            return;
        }

        Root.Data.AllWorldRecords.Node node = result.Value.data.allWorldRecords.nodes.First();

        ChatApi.SendMessage("World record: " + node.recordByRecord.time.GetFormattedTime() + " by " +
                            node.userByUser.steamName);
    }

    private class Root
    {
        public Data data { get; set; }

        public class Data
        {
            public AllWorldRecords allWorldRecords { get; set; }

            public class AllWorldRecords
            {
                public int totalCount { get; set; }
                public List<Node> nodes { get; set; }

                public class Node
                {
                    public RecordByRecord recordByRecord { get; set; }
                    public UserByUser userByUser { get; set; }

                    public class RecordByRecord
                    {
                        public double time { get; set; }
                    }

                    public class UserByUser
                    {
                        public string steamName { get; set; }
                    }
                }
            }
        }
    }
}
