using System;
using System.Threading;
using System.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Utilities;

internal static class TaskCancellation
{
    public static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            return await task;

        TaskCompletionSource<bool> cancellation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(() => cancellation.TrySetResult(true)))
        {
            if (task != await Task.WhenAny(task, cancellation.Task))
                throw new OperationCanceledException(cancellationToken);
        }

        return await task;
    }
}
