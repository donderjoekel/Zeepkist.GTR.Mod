using System;
using Serilog.Context;
using Steamworks;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Configuration;
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
    private readonly ConfigService _configService;
    private bool _pushedProperties;
    private bool _loggedIn;
    private bool _loginInProgress;
    private bool _forceLoginPending;

    public AuthenticationService(
        MessengerService messengerService,
        ApiHttpClient apiHttpClient,
        UserService userService,
        ConfigService configService)
    {
        _messengerService = messengerService;
        _apiHttpClient = apiHttpClient;
        _userService = userService;
        _configService = configService;

        MainMenuUi_Awake.Postfixed += OnMainMenuAwake;
        _configService.BackendUrl.SettingChanged += OnBackendUrlChanged;
    }

    private void OnMainMenuAwake()
    {
        if (!_pushedProperties)
        {
            _pushedProperties = true;
            GlobalLogContext.PushProperty("steam_id", SteamClient.SteamId);
            GlobalLogContext.PushProperty("steam_name", SteamClient.Name);
        }

        if (!_loggedIn)
            Login(false).Forget();
    }

    private void OnBackendUrlChanged(object sender, EventArgs e)
    {
        Login(true).Forget();
    }

    private async UniTaskVoid Login(bool force)
    {
        if (_loginInProgress)
        {
            if (force)
                _forceLoginPending = true;
            return;
        }

        _loginInProgress = true;
        bool loggedIn;
        try
        {
            loggedIn = await _apiHttpClient.Login(force);
        }
        finally
        {
            _loginInProgress = false;
        }

        if (loggedIn)
        {
            _loggedIn = true;
            _messengerService.LogSuccess("Logged in!");
        }
        else
        {
            _loggedIn = false;
            _messengerService.LogError("Failed to log in");
        }

        if (_forceLoginPending)
        {
            _forceLoginPending = false;
            Login(true).Forget();
        }
    }
}
