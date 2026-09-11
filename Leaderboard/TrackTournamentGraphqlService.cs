using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using StrawberryShake;
using TNRD.Zeepkist.GTR.GraphQL;
using ZeepSDK.External.Cysharp.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public sealed class TrackTournamentGraphqlService
{
    private readonly IGtrClient _gtrClient;

    public TrackTournamentGraphqlService(IGtrClient gtrClient)
    {
        _gtrClient = gtrClient;
    }

    public async UniTask<IReadOnlyList<TrackTournamentDescriptor>> GetForLevelAsync(
        LevelGraphqlIdentity level,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        IOperationResult<IGetLevelTrackTournamentsResult> result =
            await _gtrClient.GetLevelTrackTournaments.ExecuteAsync(
                level.XxHash,
                level.Hash,
                now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                cancellationToken);
        result.EnsureNoErrors();

        return result.Data?.TrackTournaments?.Nodes
                   .Select(Map)
                   .Where(x => x != null)
                   .OrderBy(x => x.Type)
                   .ToList() ?? new List<TrackTournamentDescriptor>();
    }

    public IDisposable WatchPage(
        TrackTournamentDescriptor tournament,
        int page,
        int pageSize,
        Action<TrackTournamentPageSnapshot> onNext,
        Action<Exception> onError)
    {
        return _gtrClient.WatchTrackTournamentPage
            .Watch(tournament.Id, pageSize, page * pageSize)
            .Subscribe(new OperationObserver<IOperationResult<IWatchTrackTournamentPageResult>>(
                result =>
                {
                    try
                    {
                        result.EnsureNoErrors();
                        onNext(Map(result.Data?.TrackTournament, tournament));
                    }
                    catch (Exception e)
                    {
                        onError(e);
                    }
                },
                onError));
    }

    private static TrackTournamentDescriptor Map(IGetLevelTrackTournaments_TrackTournaments_Nodes node)
    {
        return TryMap(node.Id, node.Type, node.Slug, node.StartAt, node.EndAt);
    }

    private static TrackTournamentPageSnapshot Map(
        IWatchTrackTournamentPage_TrackTournament data,
        TrackTournamentDescriptor fallback)
    {
        if (data == null)
            return null;

        TrackTournamentDescriptor tournament =
            TryMap(data.Id, data.Type, data.Slug, data.StartAt, data.EndAt) ?? fallback;
        IReadOnlyList<TrackTournamentStanding> records = data.TrackTournamentResults?.Nodes
            .Select(node => new TrackTournamentStanding
            {
                Rank = node.Rank,
                SteamId = node.User?.SteamId,
                SteamName = node.User?.SteamName,
                Time = node.Time,
                Points = node.Points
            })
            .ToList() ?? new List<TrackTournamentStanding>();

        return new TrackTournamentPageSnapshot
        {
            Tournament = tournament,
            TotalRecords = data.TrackTournamentResults?.TotalCount ?? 0,
            Records = records
        };
    }

    private static TrackTournamentDescriptor TryMap(
        int id,
        int type,
        string slug,
        string startAt,
        string endAt)
    {
        if (type is < (int)TrackTournamentType.Weekly or > (int)TrackTournamentType.Monthly ||
            !TryParseDate(startAt, out DateTimeOffset start) ||
            !TryParseDate(endAt, out DateTimeOffset end))
        {
            return null;
        }

        return new TrackTournamentDescriptor
        {
            Id = id,
            Type = (TrackTournamentType)type,
            Slug = slug,
            StartAt = start,
            EndAt = end
        };
    }

    private static bool TryParseDate(string value, out DateTimeOffset date)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
            out date);
    }
}
