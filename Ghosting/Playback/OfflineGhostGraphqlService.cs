using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using StrawberryShake;
using TNRD.Zeepkist.GTR.Configuration;
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
        List<string> steamIds, string levelHash)
    {
        IOperationResult<IGetAdditionalGhostsResult> result =
            await GtrClient.GetAdditionalGhosts.ExecuteAsync(steamIds, levelHash);

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
