using System;
using System.Collections.Generic;
using System.Linq;
using StrawberryShake;
using TNRD.Zeepkist.GTR.GraphQL;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class LeaderboardGraphqlService
{
    private readonly IGtrClient _gtrClient;

    public LeaderboardGraphqlService(IGtrClient gtrClient)
    {
        _gtrClient = gtrClient;
    }

    public IDisposable WatchPage(
        LevelGraphqlIdentity level,
        int page,
        int pageSize,
        Action<LeaderboardPageSnapshot> onNext,
        Action<Exception> onError)
    {
        return _gtrClient.WatchLeaderboardPage
            .Watch(level.XxHash, level.Hash, pageSize, page * pageSize)
            .Subscribe(new OperationObserver<IOperationResult<IWatchLeaderboardPageResult>>(
                result =>
                {
                    try
                    {
                        result.EnsureNoErrors();
                        onNext(Map(result.Data?.Query));
                    }
                    catch (Exception e)
                    {
                        onError(e);
                    }
                },
                onError));
    }

    private static LeaderboardPageSnapshot Map(IWatchLeaderboardPage_Query data)
    {
        IReadOnlyList<LeaderboardRecord> records = data?.Records?.Nodes
            .Select(record =>
            {
                IWatchLeaderboardPage_Query_Records_Nodes_UserPointContributions_Nodes contribution =
                    record.UserPointContributions?.Nodes.FirstOrDefault();
                return new LeaderboardRecord
                {
                    SteamId = record.User?.SteamId,
                    SteamName = record.User?.SteamName,
                    Time = record.Time,
                    DateCreated = record.DateCreated,
                    LevelPosition = contribution?.LevelPosition,
                    LevelDecayedPoints = contribution?.LevelDecayedPoints
                };
            })
            .ToList() ?? new List<LeaderboardRecord>();

        return new LeaderboardPageSnapshot
        {
            LevelName = data?.Records?.Nodes.FirstOrDefault()?.Level?.LevelItems?.Nodes.FirstOrDefault()?.Name,
            TotalRecords = data?.Records?.TotalCount ?? 0,
            Records = records
        };
    }
}
