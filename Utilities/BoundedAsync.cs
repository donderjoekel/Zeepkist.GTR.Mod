using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Utilities;

internal static class BoundedAsync
{
    public static async Task ForEachAsync<T>(
        IReadOnlyList<T> items, int concurrency, Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        int next = -1;
        var workers = new Task[Math.Min(concurrency, items.Count)];
        for (int i = 0; i < workers.Length; i++)
            workers[i] = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    int index = Interlocked.Increment(ref next);
                    if (index >= items.Count)
                        return;
                    await action(items[index], cancellationToken).ConfigureAwait(false);
                }
                cancellationToken.ThrowIfCancellationRequested();
            });
        await Task.WhenAll(workers).ConfigureAwait(false);
    }
}
