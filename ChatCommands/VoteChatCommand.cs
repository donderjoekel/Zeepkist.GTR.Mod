using System;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.SDK;

namespace TNRD.Zeepkist.GTR.Mod.ChatCommands;

public class VoteChatCommand : IChatCommand
{
    /// <inheritdoc />
    public bool CanHandle(string input)
    {
        return input.StartsWith("/vote");
    }

    /// <inheritdoc />
    public void Handle(OnlineChatUI instance, string input)
    {
        string[] splits = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (splits.Length != 3)
        {
            WriteFormatMessage(instance);
            return;
        }

        if (!TryGetCategory(splits[1], out int category))
        {
            WriteFormatMessage(instance);
            return;
        }

        if (!int.TryParse(splits[2], out int score))
        {
            WriteFormatMessage(instance);
            return;
        }

        CastVote(category, score).Forget();
    }

    private static void WriteFormatMessage(OnlineChatUI instance)
    {
        string msg = "[GTR] The format for voting is: /vote category score";
        instance.UpdateChatFields(msg, 0);
    }

    private static bool TryGetCategory(string input, out int category)
    {
        if (int.TryParse(input, out category))
            return true;

        if (string.Equals(input, "g", StringComparison.InvariantCultureIgnoreCase) ||
            string.Equals(input, "general", StringComparison.InvariantCultureIgnoreCase))
        {
            category = 0;
            return true;
        }

        if (string.Equals(input, "f", StringComparison.InvariantCultureIgnoreCase) ||
            string.Equals(input, "flow", StringComparison.InvariantCultureIgnoreCase))
        {
            category = 1;
            return true;
        }

        if (string.Equals(input, "d", StringComparison.InvariantCultureIgnoreCase) ||
            string.Equals(input, "difficulty", StringComparison.InvariantCultureIgnoreCase))
        {
            category = 2;
            return true;
        }

        if (string.Equals(input, "s", StringComparison.InvariantCultureIgnoreCase) ||
            string.Equals(input, "scenery", StringComparison.InvariantCultureIgnoreCase))
        {
            category = 3;
            return true;
        }

        return false;
    }

    private static async UniTaskVoid CastVote(int category, int score)
    {
        Result result = await VotesApi.Submit(builder =>
            builder.WithLevel(InternalLevelApi.CurrentLevelId).WithCategory(category).WithScore(score));
        if (result.IsSuccess)
        {
            PlayerManager.Instance.messenger.Log("[GTR] Vote submitted", 2.5f);
        }
        else
        {
            Plugin.Log.LogError(result.ToString());
            PlayerManager.Instance.messenger.LogError("[GTR] Failed to submit vote", 2.5f);
        }
    }
}
