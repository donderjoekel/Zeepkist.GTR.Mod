using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Discord.Wrapper;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.Users;
using ZeepSDK.External.Cysharp.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Discord;

public class DiscordService : IEagerService
{
    private readonly UserService _userService;
    private readonly Wrapper.Discord _discordClient;
    private readonly ILogger<DiscordService> _logger;
    private readonly MessengerService _messengerService;

    private bool _isLinking;

    public DiscordService(ConfigService configService,
        UserService userService,
        ILogger<DiscordService> logger,
        MessengerService messengerService)
    {
        _userService = userService;
        _logger = logger;
        _messengerService = messengerService;

        configService.ButtonLinkDiscord.SettingChanged += OnLinkDiscord;
        configService.ButtonUnlinkDiscord.SettingChanged += OnUnlinkDiscord;

        _discordClient = new Wrapper.Discord(1106610501674348554, (ulong)CreateFlags.NoRequireDiscord);
    }

    private void OnLinkDiscord(object sender, EventArgs e)
    {
        if (_isLinking)
            return;

        LinkDiscord().Forget();
    }

    private void OnUnlinkDiscord(object sender, EventArgs e)
    {
        _userService.UpdateDiscord(-1).Forget();
    }

    private async UniTaskVoid LinkDiscord()
    {
        _isLinking = true;
        try
        {
            CancellationTokenSource cts = new();

            RunDiscordCallbacks(cts.Token).Forget();

            User? currentUser = null;
            Exception ex = null;

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    ex = null;
                    UserManager userManager = _discordClient.GetUserManager();
                    currentUser = userManager.GetCurrentUser();
                    break;
                }
                catch (Exception e)
                {
                    ex = e;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(1));
            }

            if (currentUser != null)
            {
                _userService.UpdateDiscord(currentUser.Value.Id).Forget();
            }
            else
            {
                if (ex != null)
                {
                    _logger.LogError(ex, "Failed to get discord user");
                }

                _messengerService.LogError("Failed to link discord");
            }

            cts.Cancel();
        }
        finally
        {
            _isLinking = false;
        }
    }

    private async UniTaskVoid RunDiscordCallbacks(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            _discordClient.RunCallbacks();
            await UniTask.DelayFrame(1);
        }
    }
}