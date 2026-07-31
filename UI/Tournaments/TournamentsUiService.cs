using TNRD.Zeepkist.GTR.Core;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Tournaments;

public class TournamentsUiService : IEagerService
{
    public TournamentsUiService(
        TournamentsDrawer tournamentsDrawer,
        TournamentJoinLoadingDrawer loadingDrawer)
    {
        UIApi.AddZeepGUIDrawer(tournamentsDrawer);
        UIApi.AddZeepGUIDrawer(loadingDrawer);
    }
}
