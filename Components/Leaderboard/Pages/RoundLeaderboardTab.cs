using System.Linq;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Mod.Components.Leaderboard.Pages;

internal class RoundLeaderboardTab : OfficialGameLeaderboardTab
{
    /// <inheritdoc />
    protected override string GetLeaderboardTitle()
    {
        return I2.Loc.LocalizationManager.GetTranslation("Online/Leaderboard/RoundLB");
    }

    /// <inheritdoc />
    protected override ZeepkistNetworkPlayer[] GetOrderedPlayers()
    {
        return ZeepkistNetwork.PlayerList
            .OrderBy(p => p.CurrentResult?.Time ?? float.MaxValue)
            .ToArray();
    }

    /// <inheritdoc />
    protected override bool ShouldShowPosition(ZeepkistNetworkPlayer player)
    {
        return player.CurrentResult != null;
    }
}
