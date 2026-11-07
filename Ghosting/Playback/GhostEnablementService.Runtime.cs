using System;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Multiplayer;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public sealed partial class GhostEnablementService : IEagerService
{
    public GhostEnablementService(ConfigService config, OnlineGhostsService online,
        OfflineGhostsService offline, GhostPlayer player, GhostPlaybackService playback)
        : this(() => config.EnableGhosts.Value,
            handler => config.EnableGhosts.SettingChanged += handler,
            handler => config.EnableGhosts.SettingChanged -= handler,
            action => RunOnMainThread(action).Forget(),
            () => { online.CancelPendingLoads(); offline.CancelPendingLoads(); },
            () => { playback.Stop(); player.ClearGhosts(); },
            () =>
            {
                if (PlayerManager.Instance?.currentMaster == null) return;
                if (MultiplayerApi.IsPlayingOnline) online.ReloadCurrentLevel();
                else offline.ReloadCurrentLevel();
            })
    {
    }

    private static async UniTaskVoid RunOnMainThread(Action action)
    {
        await UniTask.SwitchToMainThread();
        action();
    }
}
