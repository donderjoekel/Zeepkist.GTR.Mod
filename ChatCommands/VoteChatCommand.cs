using System;
using BepInEx.Logging;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.SDK;

namespace TNRD.Zeepkist.GTR.Mod.ChatCommands;

public class VoteChatCommand : IChatCommand
{
    private static ManualLogSource logger;
    
    public VoteChatCommand()
    {
        logger = EntryPoint.CreateLogger(nameof(VoteChatCommand));
    }
    
    /// <inheritdoc />
    public bool CanHandle(string input)
    {
        return input.StartsWith("/vote");
    }

    /// <inheritdoc />
    public void Handle(OnlineChatUI instance, string input)
    {
        string[] splits = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (splits.Length != 2)
        {
            WriteFormatMessage(instance);
            return;
        }

        if (!int.TryParse(splits[1], out int score))
        {
            WriteFormatMessage(instance);
            return;
        }

        CastVote(score).Forget();
    }

    private static void WriteFormatMessage(OnlineChatUI instance)
    {
        string msg = "[GTR] The format for voting is: /vote score";
        instance.UpdateChatFields(msg, 0);
    }

    private static async UniTaskVoid CastVote(int score)
    {
        Result result = await Sdk.Instance.VotesApi.Submit(builder =>
            builder.WithLevel(InternalLevelApi.CurrentLevelId).WithScore(score));
        if (result.IsSuccess)
        {
            PlayerManager.Instance.messenger.Log("[GTR] Vote submitted", 2.5f);
        }
        else
        {
            logger.LogError(result.ToString());
            PlayerManager.Instance.messenger.LogError("[GTR] Failed to submit vote", 2.5f);
        }
    }
}
