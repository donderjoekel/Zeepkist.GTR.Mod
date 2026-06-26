using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using StrawberryShake;
using TNRD.Zeepkist.GTR.GraphQL;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class LeaderboardGraphqlService
{
    private readonly IGtrClient _gtrClient;
    private readonly IMemoryCache _cache;

    public LeaderboardGraphqlService(IGtrClient gtrClient, IMemoryCache cache)
    {
        _gtrClient = gtrClient;
        _cache = cache;
    }

    public async UniTask<Result<int>> GetPersonalBestCount(LevelGraphqlIdentity level, CancellationToken ct = default)
    {
        try
        {
            IOperationResult<IGetPersonalBestCountResult> result =
                await _gtrClient.GetPersonalBestCount.ExecuteAsync(level.XxHash, level.Hash, ct);
            try
            {
                result.EnsureNoErrors();
            }
            catch (Exception e)
            {
                return Result.Fail(new ExceptionalError(e));
            }

            int? totalCount = result.Data?.PersonalBestGlobals?.TotalCount;
            return Result.Ok(totalCount ?? 0);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }
    }

    public async UniTask<Result<int>> GetTotalUserCount(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CreateKey(), out int cachedCount))
            return cachedCount;

        IOperationResult<IGetTotalUserCountResult> result = await _gtrClient.GetTotalUserCount.ExecuteAsync(ct);
        try
        {
            result.EnsureNoErrors();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        int? totalUserCount = result.Data?.Users?.TotalCount;
        if (totalUserCount.HasValue)
        {
            _cache.Set(CreateKey(), totalUserCount.Value, TimeSpan.FromMinutes(30));
            return totalUserCount.Value;
        }

        _cache.Set(CreateKey(), 0, TimeSpan.FromSeconds(30));
        return 0;

        string CreateKey()
        {
            return "TotalUserCount";
        }
    }

    public async UniTask<Result<int?>> GetLevelPoints(LevelGraphqlIdentity level, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CreateKey(), out int? cachedPoints))
            return cachedPoints;

        IOperationResult<IGetLevelPointsResult> result =
            await _gtrClient.GetLevelPoints.ExecuteAsync(level.XxHash, level.Hash, ct);
        try
        {
            result.EnsureNoErrors();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        int? points = result.Data?.LevelPoints?.Nodes.FirstOrDefault()?.Points;

        if (points.HasValue)
        {
            _cache.Set(CreateKey(), points.Value, TimeSpan.FromMinutes(5));
            return points.Value;
        }

        _cache.Set<int?>(CreateKey(), null, TimeSpan.FromMinutes(1));
        return null;

        string CreateKey()
        {
            return $"LevelPoints-{level.CacheKey}";
        }
    }

    public async UniTask<Result<IGetPersonalBestsResult>> GetLeaderboardRecords(
        LevelGraphqlIdentity level,
        int page = 0,
        CancellationToken ct = default,
        int pageSize = 16)
    {
        IOperationResult<IGetPersonalBestsResult> result =
            await _gtrClient.GetPersonalBests.ExecuteAsync(level.XxHash, level.Hash, pageSize, page * pageSize, ct);

        try
        {
            result.EnsureNoErrors();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        return Result.Ok(result.Data);
    }
}
