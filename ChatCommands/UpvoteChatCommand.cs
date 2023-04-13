using System;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.SDK;

namespace TNRD.Zeepkist.GTR.Mod.ChatCommands;

public class UpvoteChatCommand : IChatCommand
{
    /// <inheritdoc />
    public bool CanHandle(string input)
    {
        return input.StartsWith("/upvote", StringComparison.InvariantCultureIgnoreCase) ||
               input.StartsWith("/up", StringComparison.InvariantCultureIgnoreCase) ||
               input.StartsWith("/uv", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <inheritdoc />
    public void Handle(OnlineChatUI instance, string input)
    {
        int currentLevelId = InternalLevelApi.CurrentLevelId;
        Submit(currentLevelId).Forget();
    }

    private static async UniTaskVoid Submit(int level)
    {
        Result<GenericIdResponseDTO> result = await UpvotesApi.Add(builder => builder.WithLevelId(level));
        if (result.IsSuccess)
        {
            PlayerManager.Instance.messenger.Log("[GTR] Upvote success", 2.5f);
        }
        else
        {
            PlayerManager.Instance.messenger.LogError("[GTR] Failed to upvote", 2.5f);
        }
    }
}
