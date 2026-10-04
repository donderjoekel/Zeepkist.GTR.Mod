using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;
using TNRD.Zeepkist.GTR.Utilities;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

/// <summary>One creation budget shared by online and offline producers.</summary>
public sealed class GhostLoadDispatcher : IEagerService, IDisposable
{
    private readonly BoundedMainThreadQueue<Action> _pending = new(4);
    private readonly PlayerLoopService _playerLoop;
    private readonly PlayerLoopSubscription _update;
    private readonly ILogger<GhostLoadDispatcher> _logger;

    public GhostLoadDispatcher(PlayerLoopService playerLoop, ILogger<GhostLoadDispatcher> logger)
    {
        _playerLoop = playerLoop;
        _logger = logger;
        _update = playerLoop.SubscribeUpdate(Drain);
    }

    public Task EnqueueAsync(Action operation, CancellationToken token) => _pending.EnqueueAsync(operation, token);

    // Reserve capacity before decoding. Producers cannot retain decoded ghosts outside the queue.
    public Task PrepareAsync(Func<CancellationToken, Task<Action>> prepare, CancellationToken token) =>
        _pending.EnqueuePreparedAsync(prepare, token);

    public void DiscardCancelled() => _pending.DiscardCancelled();

    private void Drain()
    {
        int processed = 0;
        long start = Stopwatch.GetTimestamp();
        while (GhostLoadBudget.CanProcessNext(
                   processed, (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency) &&
               _pending.TryDequeue(out Action operation))
        {
            try { operation(); }
            catch (Exception error) { _logger.LogWarning(error, "Ghost load operation failed"); }
            processed++;
        }
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _playerLoop.UnsubscribeUpdate(_update);
        _pending.Dispose();
    }
}
