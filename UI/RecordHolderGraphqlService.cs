using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using StrawberryShake;
using TNRD.Zeepkist.GTR.GraphQL;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.UI;

public class RecordHolderGraphqlService
{
    private readonly ILogger<RecordHolderGraphqlService> _logger;
    private readonly IGtrClient _gtrClient;

    public RecordHolderGraphqlService(ILogger<RecordHolderGraphqlService> logger, IGtrClient gtrClient)
    {
        _logger = logger;
        _gtrClient = gtrClient;
    }

    public async UniTask<Result<IGetWorldRecordHolder_WorldRecordGlobals_Nodes>> GetWorldRecordHolder(
        LevelGraphqlIdentity level,
        CancellationToken ct)
    {
        IOperationResult<IGetWorldRecordHolderResult> result =
            await _gtrClient.GetWorldRecordHolder.ExecuteAsync(level.XxHash, level.Hash, ct);

        try
        {
            result.EnsureNoErrors();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        IReadOnlyList<IGetWorldRecordHolder_WorldRecordGlobals_Nodes>
            nodes = result.Data.WorldRecordGlobals.Nodes;

        return nodes.Count > 0 ? Result.Ok(nodes.First()) : Result.Ok();
    }

    public async UniTask<Result<IGetPersonalBest_PersonalBestGlobals_Nodes>> GetPersonalBestHolder(LevelGraphqlIdentity level,
        ulong steamId, CancellationToken ct)
    {
        IOperationResult<IGetPersonalBestResult> result =
            await _gtrClient.GetPersonalBest.ExecuteAsync(level.XxHash, level.Hash, steamId.ToString(), ct);

        try
        {
            result.EnsureNoErrors();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        IReadOnlyList<IGetPersonalBest_PersonalBestGlobals_Nodes> nodes = result.Data.PersonalBestGlobals.Nodes;

        return nodes.Count > 0 ? Result.Ok(nodes.First()) : Result.Ok();
    }

    public async UniTask<Result<int>> GetRank(LevelGraphqlIdentity level, double time, CancellationToken ct)
    {
        IOperationResult<IGetPlayerRankOnLevelResult> result =
            await _gtrClient.GetPlayerRankOnLevel.ExecuteAsync(level.XxHash, level.Hash, time, ct);

        try
        {
            result.EnsureNoErrors();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        return Result.Ok(result.Data!.Records!.TotalCount + 1);
    }
}
