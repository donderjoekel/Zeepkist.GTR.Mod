using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.Patching.Patches;
using TNRD.Zeepkist.GTR.Users;
using ZeepSDK.External.Cysharp.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Authentication;

public class AuthenticationService : IEagerService
{
    private readonly MessengerService _messengerService;
    private readonly ApiHttpClient _apiHttpClient;
    private readonly UserService _userService;

    public AuthenticationService(
        MessengerService messengerService,
        ApiHttpClient apiHttpClient,
        UserService userService)
    {
        _messengerService = messengerService;
        _apiHttpClient = apiHttpClient;
        _userService = userService;

        MainMenuUi_Awake.Postfixed += OnMainMenuAwake;
    }

    private void OnMainMenuAwake()
    {
        Login().Forget();
    }

    private async UniTaskVoid Login()
    {
        if (await _apiHttpClient.Login())
        {
            _messengerService.LogSuccess("Logged in!");
            _userService.UpdateName().Forget();
        }
        else
        {
            _messengerService.LogError("Failed to log in");
        }
    }
}
