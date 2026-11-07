using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using StrawberryShake;
using TNRD.Zeepkist.GTR.Core;

namespace TNRD.Zeepkist.GTR.GraphQL;

internal static class GtrVersionCheck
{
    public static async Task<GtrVersionStatus> ExecuteAsync(
        IServiceProvider services, string currentVersion, CancellationToken cancellationToken)
    {
        // Resolving IGtrClient also resolves every subscription and requires a WebSocket session pool.
        // Resolve only this HTTP query so bootstrap never creates subscription services.
        var result = await services.GetRequiredService<GTR_Version_CheckQuery>().ExecuteAsync(cancellationToken);
        result.EnsureNoErrors();
        var version = result.Data?.Versions?.Nodes.FirstOrDefault();
        return GtrVersionStatus.Evaluate(currentVersion, version?.Minimum, version?.Latest);
    }
}
