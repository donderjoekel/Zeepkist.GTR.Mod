using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Configuration;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class OfflineGhostGraphqlService : OnlineGhostGraphqlService
{
    private const string Query
        = "fragment frag on Record{id userByIdUser{steamName}recordMediasByIdRecord{nodes{ghostUrl}}}query GetAdditionalGhosts($ids:[BigFloat!],$hash:String){allPersonalBestGlobals(filter:{levelByIdLevel:{hash:{equalTo:$hash}}userByIdUser:{steamId:{in:$ids}}}){nodes{recordByIdRecord{...frag}}}}";

    public OfflineGhostGraphqlService(GraphQLApiHttpClient client, ConfigService configService)
        : base(client, configService)
    {
    }

    public async UniTask<Result<List<PersonalBest>>> GetAdditionalGhosts(List<string> steamIds, string levelHash)
    {
        Result<Root> result = await Client.PostAsync<Root>(
            Query,
            new
            {
                ids = steamIds.ToArray(),
                hash = levelHash
            });

        if (result.IsFailed)
        {
            return result.ToResult();
        }

        return Result.Ok(Map(result.Value));
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
