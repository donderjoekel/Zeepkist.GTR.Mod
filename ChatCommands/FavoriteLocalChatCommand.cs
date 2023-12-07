using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using ZeepSDK.ChatCommands;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Mod.ChatCommands;

public class FavoriteLocalChatCommand : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "favorite";
    public string Description => "Favorites the current level on GTR";

    public void Handle(string arguments)
    {
        Submit(InternalLevelApi.CurrentLevelHash).Forget();
    }

    private static async UniTaskVoid Submit(string level)
    {
        if (string.IsNullOrEmpty(level))
        {
            LoggerFactory.GetLogger<FavoriteLocalChatCommand>().LogWarning("Unable to favorite level, no level loaded");
            return;
        }

        Result<GenericIdResponseDTO> result =
            await SdkWrapper.Instance.FavoritesApi.Add(builder => builder.WithLevel(level));
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
