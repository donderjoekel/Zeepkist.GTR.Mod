using System;
using System.Threading;
using System.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Core;

// No operational services may be created before this gate completes.
internal static class GtrStartupGate
{
    public static async Task<GtrVersionStatus> CheckAsync(
        string current,
        Func<Task> trace,
        Func<CancellationToken, Task<GtrVersionStatus>> check,
        Action<Exception> onFailure,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await trace();
            cancellationToken.ThrowIfCancellationRequested();
            return await check(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            onFailure(exception);
            return GtrVersionStatus.Unknown(current);
        }
    }
}
