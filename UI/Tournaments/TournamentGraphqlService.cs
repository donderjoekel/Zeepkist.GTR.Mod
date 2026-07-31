using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using StrawberryShake;
using TNRD.Zeepkist.GTR.GraphQL;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.UI.Tournaments;

public class TournamentGraphqlService
{
    private readonly ILogger<TournamentGraphqlService> _logger;
    private readonly IGtrClient _gtrClient;

    public TournamentGraphqlService(ILogger<TournamentGraphqlService> logger, IGtrClient gtrClient)
    {
        _logger = logger;
        _gtrClient = gtrClient;
    }

    public async UniTask<Result<IReadOnlyList<TournamentViewModel>>> GetActiveTournaments(CancellationToken ct)
    {
        string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        IOperationResult<IGetActiveTrackTournamentsResult> result =
            await _gtrClient.GetActiveTrackTournaments.ExecuteAsync(now, ct);

        try
        {
            result.EnsureNoErrors();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to fetch active track tournaments");
            return Result.Fail(new ExceptionalError(e));
        }

        IReadOnlyList<IGetActiveTrackTournaments_TrackTournaments_Nodes> nodes =
            result.Data?.TrackTournaments?.Nodes ?? Array.Empty<IGetActiveTrackTournaments_TrackTournaments_Nodes>();

        List<TournamentViewModel> tournaments = new();
        foreach (IGetActiveTrackTournaments_TrackTournaments_Nodes node in nodes)
        {
            IGetActiveTrackTournaments_TrackTournaments_Nodes_Level_LevelItems_Nodes levelItem =
                node.Level?.LevelItems?.Nodes?.FirstOrDefault();
            if (levelItem == null)
            {
                _logger.LogWarning("Skipping tournament {Slug} ({Id}): missing level item", node.Slug, node.Id);
                continue;
            }

            if (!ulong.TryParse(levelItem.WorkshopId, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out ulong workshopId))
            {
                _logger.LogWarning("Skipping tournament {Slug} ({Id}): invalid workshopId {WorkshopId}",
                    node.Slug, node.Id, levelItem.WorkshopId);
                continue;
            }

            IGetActiveTrackTournaments_TrackTournaments_Nodes_TrackTournamentResults results =
                node.TrackTournamentResults;
            List<TournamentStandingEntry> standings = new();
            if (results?.Nodes != null)
            {
                foreach (IGetActiveTrackTournaments_TrackTournaments_Nodes_TrackTournamentResults_Nodes entry in
                         results.Nodes)
                {
                    standings.Add(new TournamentStandingEntry
                    {
                        Rank = entry.Rank,
                        PlayerName = string.IsNullOrWhiteSpace(entry.User?.SteamName)
                            ? "Unknown"
                            : entry.User.SteamName,
                        Time = (float)entry.Time,
                        Points = entry.Points
                    });
                }
            }

            tournaments.Add(new TournamentViewModel
            {
                Id = node.Id,
                Type = node.Type,
                Slug = node.Slug,
                StartAt = ParseDateTime(node.StartAt),
                EndAt = ParseDateTime(node.EndAt),
                LevelName = levelItem.Name,
                LevelAuthor = levelItem.FileAuthor,
                LevelUid = levelItem.FileUid,
                WorkshopId = workshopId,
                ImageUrl = levelItem.ImageUrl,
                ValidationTimeAuthor = (float)levelItem.ValidationTimeAuthor,
                ValidationTimeGold = (float)levelItem.ValidationTimeGold,
                ValidationTimeSilver = (float)levelItem.ValidationTimeSilver,
                ValidationTimeBronze = (float)levelItem.ValidationTimeBronze,
                StandingCount = results?.TotalCount ?? 0,
                Standings = standings
            });
        }

        return Result.Ok<IReadOnlyList<TournamentViewModel>>(tournaments);
    }

    private static DateTime ParseDateTime(string value)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
            return parsed.ToUniversalTime();

        return DateTime.MinValue;
    }
}
