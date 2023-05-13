using System;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.SDK;

namespace TNRD.Zeepkist.GTR.Mod.ChatCommands;

public class FavoriteChatCommand : IChatCommand
{
    /// <inheritdoc />
    public bool CanHandle(string input)
    {
        return input.StartsWith("/favorite", StringComparison.InvariantCultureIgnoreCase) ||
               input.StartsWith("/fav", StringComparison.InvariantCultureIgnoreCase) ||
               input.StartsWith("/favo", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <inheritdoc />
    public void Handle(OnlineChatUI instance, string input)
    {
        int currentLevelId = InternalLevelApi.CurrentLevelId;
        Submit(currentLevelId).Forget();
    }

    private static async UniTaskVoid Submit(int level)
    {
        Result<GenericIdResponseDTO> result = await Sdk.Instance.FavoritesApi.Add(builder => builder.WithLevelId(level));
        if (result.IsSuccess)
        {
            PlayerManager.Instance.messenger.Log("[GTR] Favorite success", 2.5f);
        }
        else
        {
            PlayerManager.Instance.messenger.LogError("[GTR] Failed to favorite", 2.5f);
        }
    }
}
