using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Utilities;

/// <summary>Shares work, not caller cancellation. Last waiter cancels abandoned work.</summary>
internal sealed class SharedAsyncLoad<TKey, TValue> : IDisposable
{
    private sealed class Entry
    {
        public readonly CancellationTokenSource Cancellation = new();
        public Task<TValue> Task;
        public int Waiters;
    }

    internal sealed class Lease : IDisposable
    {
        private Action _release;
        public TValue Value { get; }
        internal Lease(TValue value, Action release) { Value = value; _release = release; }
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }

    private readonly object _gate = new();
    private readonly Dictionary<TKey, Entry> _entries = new();
    private readonly Func<TKey, CancellationToken, Task<TValue>> _load;
    private bool _disposed;

    public SharedAsyncLoad(Func<TKey, CancellationToken, Task<TValue>> load) => _load = load;

    public async Task<TValue> GetAsync(TKey key, CancellationToken cancellationToken)
    {
        using Lease lease = await RentAsync(key, cancellationToken).ConfigureAwait(false);
        return lease.Value;
    }

    public async Task<Lease> RentAsync(TKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Entry entry;
        lock (_gate)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SharedAsyncLoad<TKey, TValue>));
            if (!_entries.TryGetValue(key, out entry))
            {
                entry = new Entry();
                CancellationToken token = entry.Cancellation.Token;
                entry.Task = Task.Run(() => _load(key, token));
                _entries.Add(key, entry);
            }
            entry.Waiters++;
        }

        try
        {
            TValue value = await TaskCancellation.WaitAsync(entry.Task, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new Lease(value, () => Release(key, entry));
        }
        catch
        {
            Release(key, entry);
            throw;
        }
    }

    private void Release(TKey key, Entry entry)
    {
        lock (_gate)
        {
            if (--entry.Waiters == 0)
            {
                if (_entries.TryGetValue(key, out Entry current) && ReferenceEquals(current, entry))
                    _entries.Remove(key);
                // A staged decode can still be running after source fetch.
                entry.Cancellation.Cancel();
                _ = entry.Task.ContinueWith(task =>
                {
                    _ = task.Exception;
                    entry.Cancellation.Dispose();
                }, TaskScheduler.Default);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (Entry entry in _entries.Values)
                entry.Cancellation.Cancel();
            _entries.Clear();
        }
    }
}
