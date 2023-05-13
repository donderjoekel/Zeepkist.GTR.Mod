using System;
using System.Threading;
using Steamworks;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.DiscordWrapper;
using TNRD.Zeepkist.GTR.SDK.Errors;
using Result = TNRD.Zeepkist.GTR.FluentResults.Result;

namespace TNRD.Zeepkist.GTR.Mod.Api.Users;

internal class CustomUsersApi
{
    /// <summary>
    /// Attempts to update the name of the current player
    /// </summary>
    /// <returns></returns>
    internal static async UniTask<Result> UpdateName()
    {
        if (!SteamClient.IsLoggedOn)
        {
            return Result.Fail(new SteamNotLoggedOnError());
        }

        UsersUpdateNameRequestDTO requestDTO = new UsersUpdateNameRequestDTOBuilder()
            .WithSteamName(SteamClient.Name)
            .Build();

        return await Sdk.Instance.ApiClient.Post("users/name", requestDTO);
    }

    internal static async UniTask<Result> UpdateDiscordId()
    {
        if (!Sdk.Instance.DiscordApplicationId.HasValue)
            return Result.Ok();

        Discord client = new(Sdk.Instance.DiscordApplicationId.Value, (ulong)CreateFlags.NoRequireDiscord);

        CancellationTokenSource cts = new();

        RunDiscordCallbacks(client, cts.Token).Forget();

        User? currentUser = null;
        Exception ex = null;

        for (int i = 0; i < 3; i++)
        {
            try
            {
                ex = null;
                UserManager userManager = client.GetUserManager();
                currentUser = userManager.GetCurrentUser();
                break;
            }
            catch (Exception e)
            {
                ex = e;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(1));
        }

        cts.Cancel();

        if (currentUser == null)
            return ex == null ? Result.Fail(new Error("No user")) : Result.Fail(new ExceptionalError(ex));

        UsersUpdateDiscordIdRequestDTO requestDTO = new UsersUpdateDiscordIdRequestDTOBuilder()
            .WithDiscordId(currentUser.Value.Id.ToString())
            .Build();

        return await Sdk.Instance.ApiClient.Post("users/discord", requestDTO);
    }

    private static async UniTaskVoid RunDiscordCallbacks(Discord client, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            client.RunCallbacks();
            await UniTask.Yield();
        }
    }
}
