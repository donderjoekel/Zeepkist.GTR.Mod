using System.Net.Http;
using Steamworks;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Messaging;
using ZeepSDK.External.Cysharp.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Users;

public class UserService
{
    private readonly ApiHttpClient _apiHttpClient;
    private readonly MessengerService _messengerService;

    public UserService(ApiHttpClient apiHttpClient, MessengerService messengerService)
    {
        _apiHttpClient = apiHttpClient;
        _messengerService = messengerService;
    }

    public async UniTaskVoid UpdateName()
    {
        UpdateNameResource resource = new()
        {
            Name = SteamClient.Name
        };

        HttpResponseMessage response = await _apiHttpClient.PostAsync("User/update/name", resource);

        if (!response.IsSuccessStatusCode)
        {
            _messengerService.LogWarning("Failed to update name");
        }
    }

    public async UniTaskVoid UpdateDiscord(decimal id)
    {
        UpdateDiscordResource resource = new()
        {
            Id = id
        };

        HttpResponseMessage response = await _apiHttpClient.PostAsync("User/update/discord", resource);

        if (response.IsSuccessStatusCode)
        {
            if (id == -1)
            {
                _messengerService.LogSuccess("Unlinked discord");
            }
            else
            {
                _messengerService.LogSuccess("Linked discord");
            }
        }
        else
        {
            _messengerService.LogWarning("Failed to update discord");
        }
    }

    private class UpdateNameResource
    {
        public string Name { get; set; } = null!;
    }

    private class UpdateDiscordResource
    {
        public decimal Id { get; set; }
    }
}
