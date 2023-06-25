using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using ZeepSDK.ChatCommands;

namespace TNRD.Zeepkist.GTR.Mod.ChatCommands;

public class UpvoteLocalChatCommand : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "upvote";
    public string Description => "Upvotes the current level on GTR";

    public void Handle(string arguments)
    {
        int currentLevelId = InternalLevelApi.CurrentLevelId;
        Submit(currentLevelId).Forget();
    }

    private static async UniTaskVoid Submit(int level)
    {
        Result<GenericIdResponseDTO> result = await SdkWrapper.Instance.UpvotesApi.Add(builder => builder.WithLevelId(level));
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
