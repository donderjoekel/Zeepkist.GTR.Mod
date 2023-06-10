using System.Linq;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Mod.Components.Leaderboard.Pages;

internal class ChampionshipLeaderboardTab : OfficialGameLeaderboardTab
{
    /// <inheritdoc />
    protected override string GetLeaderboardTitle()
    {
        return I2.Loc.LocalizationManager.GetTranslation("Online/Leaderboard/ChampionshipLB");
    }

    /// <inheritdoc />
    protected override ZeepkistNetworkPlayer[] GetOrderedPlayers()
    {
        return ZeepkistNetwork.PlayerList
            .OrderByDescending(p => p.ChampionshipPoints.x)
            .ToArray();
    }

    /// <inheritdoc />
    protected override bool ShouldShowPosition(ZeepkistNetworkPlayer player)
    {
        return player.ChampionshipPoints.x > 0;
    }
}
