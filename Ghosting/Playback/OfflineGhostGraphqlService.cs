using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using StrawberryShake;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.GraphQL;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class OfflineGhostGraphqlService : OnlineGhostGraphqlService
{
    public OfflineGhostGraphqlService(ConfigService configService, IGtrClient gtrClient)
        : base(configService, gtrClient)
    {
    }

    public async UniTask<Result<IReadOnlyList<IGetAdditionalGhosts_PersonalBestGlobals_Nodes>>> GetAdditionalGhosts(
        List<string> steamIds,
        LevelGraphqlIdentity level,
        CancellationToken cancellationToken = default)
    {
        IOperationResult<IGetAdditionalGhostsResult> result =
            await GtrClient.GetAdditionalGhosts.ExecuteAsync(steamIds, level.XxHash, level.Hash, cancellationToken);

        try
        {
            result.EnsureNoErrors();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        IReadOnlyList<IGetAdditionalGhosts_PersonalBestGlobals_Nodes> nodes = result.Data.PersonalBestGlobals.Nodes;
        return Result.Ok(nodes);
    }

    public async UniTask<Result<IReadOnlyList<IGetAllPersonalBestGhosts_Records_Nodes>>> GetAllPersonalBestGhosts(
        LevelGraphqlIdentity level,
        int? first,
        CancellationToken cancellationToken = default)
    {
        IOperationResult<IGetAllPersonalBestGhostsResult> result =
            await GtrClient.GetAllPersonalBestGhosts.ExecuteAsync(level.XxHash, level.Hash, first, cancellationToken);

        try
        {
            result.EnsureNoErrors();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        IReadOnlyList<IGetAllPersonalBestGhosts_Records_Nodes> nodes = result.Data?.Records?.Nodes;
        return Result.Ok(nodes ?? []);
    }


    public async UniTask<Result<IReadOnlyList<IGetTopRecordGhosts_Records_Nodes>>> GetTopRecordGhosts(
        LevelGraphqlIdentity level,
        int first,
        CancellationToken cancellationToken = default)
    {
        IOperationResult<IGetTopRecordGhostsResult> result =
            await GtrClient.GetTopRecordGhosts.ExecuteAsync(level.XxHash, level.Hash, first, cancellationToken);

        try
        {
            result.EnsureNoErrors();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        IReadOnlyList<IGetTopRecordGhosts_Records_Nodes> nodes = result.Data?.Records?.Nodes;
        return Result.Ok(nodes ?? []);
    }
    private static List<PersonalBest> Map(Root root)
    {
        return Map(root.Data);
    }

    private static List<PersonalBest> Map(Data data)
    {
        return Map(data.AllPersonalBestGlobals);
    }

    private static List<PersonalBest> Map(AllPersonalBestGlobals globals)
    {
        return globals.Nodes.Select(x => Map(x.RecordByIdRecord)).ToList();
    }

    [UsedImplicitly]
    private class Root
    {
        public Data Data { get; set; }
    }

    [UsedImplicitly]
    private class Data
    {
        public AllPersonalBestGlobals AllPersonalBestGlobals { get; set; }
    }
}
