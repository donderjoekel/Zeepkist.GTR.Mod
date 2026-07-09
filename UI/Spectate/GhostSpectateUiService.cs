using TNRD.Zeepkist.GTR.Core;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Spectate;

public class GhostSpectateUiService : IEagerService
{
    public GhostSpectateUiService(GhostSpectateDrawer spectateDrawer)
    {
        UIApi.AddZeepGUIDrawer(spectateDrawer);
    }
}
