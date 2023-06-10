using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Mod.Components.Leaderboard.Pages;

internal abstract class OfficialGameLeaderboardTab : BaseLeaderboardTab
{
    /// <inheritdoc />
    protected sealed override void OnEnable()
    {
        ZeepkistNetwork.LeaderboardUpdated += OnLeaderboardUpdated;
        ZeepkistNetwork.PlayerResultsChanged += OnPlayerResultsChanged;

        MaxPages = (ZeepkistNetwork.PlayerList.Count - 1) / 16;
    }

    /// <inheritdoc />
    protected sealed override void OnDisable()
    {
        ZeepkistNetwork.LeaderboardUpdated -= OnLeaderboardUpdated;
        ZeepkistNetwork.PlayerResultsChanged -= OnPlayerResultsChanged;
    }

    private void OnPlayerResultsChanged(ZeepkistNetworkPlayer obj)
    {
        MaxPages = (ZeepkistNetwork.PlayerList.Count - 1) / 16;
    }

    private void OnLeaderboardUpdated()
    {
        MaxPages = (ZeepkistNetwork.PlayerList.Count - 1) / 16;
    }

    /// <inheritdoc />
    protected sealed override void OnDraw()
    {
        ZeepkistNetworkPlayer[] players = GetOrderedPlayers();

        for (int i = 0; i < Instance.leaderboard_tab_positions.Count; ++i)
        {
            int index = CurrentPage * 16 + i;
            if (index >= players.Length)
                continue;

            ZeepkistNetworkPlayer player = players[index];
            GUI_OnlineLeaderboardPosition item = Instance.leaderboard_tab_positions[i];

            item.position.text = (index + 1).ToString(CultureInfo.InvariantCulture);

            string formattedTime = player.CurrentResult != null
                ? player.CurrentResult.Time.GetFormattedTime()
                : string.Empty;

            if (player.ChampionshipPoints.x > 0)
            {
                Vector2Int championshipPoints = player.ChampionshipPoints;
                item.pointsCurrent.text = I2.Loc.LocalizationManager.GetTranslation("Online/Leaderboard/Points")
                    .Replace("{[POINTS]}", Mathf.Round(championshipPoints.x).ToString(CultureInfo.InvariantCulture));

                if (championshipPoints.y != 0)
                {
                    item.pointsWon.text =
                        "(+" + Mathf.Round(championshipPoints.y).ToString(CultureInfo.InvariantCulture) + ")";
                }
            }

            if (player.IsMaster)
            {
                item.DrawLeaderboard(player.SteamID,
                    string.Format(
                        "<link=\"{0}\"><sprite=\"achievement 2\" name=\"host_client\"><#FFC980>{1}</color></link>",
                        player.SteamID,
                        Instance.Filter(player.Username.NoParse(), Steam_TheAchiever.FilterPurpose.player)));
            }
            else
            {
                item.DrawLeaderboard(player.SteamID,
                    string.Format("<link=\"{0}\">{1}</link>",
                        player.SteamID,
                        Instance.Filter(player.Username.NoParse(), Steam_TheAchiever.FilterPurpose.player)));
            }

            item.time.text = formattedTime;
            if (ShouldShowPosition(player))
            {
                item.position.gameObject.SetActive(true);
                item.position.color =
                    PlayerManager.Instance.GetColorFromPosition(index + 1);
            }
            else
            {
                item.position.gameObject.SetActive(false);
            }
        }
    }

    protected abstract ZeepkistNetworkPlayer[] GetOrderedPlayers();

    protected abstract bool ShouldShowPosition(ZeepkistNetworkPlayer player);
}
