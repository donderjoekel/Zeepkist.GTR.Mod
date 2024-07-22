using System.Threading;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using ZeepSDK.External.Cysharp.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class OfflineGhostsService : IEagerService
{
    public async UniTask<IGhost> LoadGhost(CancellationToken ct = default)
    {
        return null;
    }
}
