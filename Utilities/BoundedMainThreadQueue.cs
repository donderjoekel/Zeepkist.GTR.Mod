using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Utilities;

internal sealed class BoundedMainThreadQueue<T> : IDisposable
{
    private readonly Queue<(T Item, CancellationToken Token)> _items = new();
    private readonly object _gate = new();
    private readonly SemaphoreSlim _slots;
    private readonly CancellationTokenSource _shutdown = new();
    private bool _disposed;

    public BoundedMainThreadQueue(int capacity) => _slots = new SemaphoreSlim(capacity, capacity);

    public Task EnqueueAsync(T item, CancellationToken cancellationToken) =>
        EnqueuePreparedAsync(_ => Task.FromResult(item), cancellationToken);

    public async Task EnqueuePreparedAsync(Func<CancellationToken, Task<T>> prepare, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        await _slots.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            T item = await prepare(linked.Token).ConfigureAwait(false);
            lock (_gate)
            {
                if (_disposed || linked.IsCancellationRequested)
                    throw new OperationCanceledException(linked.Token);
                _items.Enqueue((item, cancellationToken));
            }
        }
        catch { _slots.Release(); throw; }
    }

    public bool TryDequeue(out T item)
    {
        lock (_gate)
        {
            while (_items.Count > 0)
            {
                var entry = _items.Dequeue();
                _slots.Release();
                if (entry.Token.IsCancellationRequested)
                    continue;
                item = entry.Item;
                return true;
            }
        }
        item = default;
        return false;
    }

    public void DiscardCancelled()
    {
        lock (_gate)
        {
            int count = _items.Count;
            for (int i = 0; i < count; i++)
            {
                var entry = _items.Dequeue();
                if (entry.Token.IsCancellationRequested)
                    _slots.Release();
                else
                    _items.Enqueue(entry);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _shutdown.Cancel();
            while (_items.Count > 0)
            {
                _items.Dequeue();
                _slots.Release();
            }
        }
        // Waiters may still unwind; leave synchronization objects usable until collected.
    }
}
