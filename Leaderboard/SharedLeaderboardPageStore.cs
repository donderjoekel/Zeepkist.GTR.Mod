using System;
using System.Collections.Generic;
using System.Linq;

namespace TNRD.Zeepkist.GTR.Leaderboard;

/// <summary>Main-thread page sharing. The UI prefetch lease keeps the first page live across tab switches.</summary>
internal sealed class SharedLeaderboardPageStore<T> : IDisposable
{
    private readonly Dictionary<string, Page> _pages = new();

    public IDisposable Watch(string key, Func<Action<T>, Action<Exception>, IDisposable> connect,
        Action<T> onNext, Action<Exception> onError)
    {
        bool first = !_pages.TryGetValue(key, out Page page);
        if (first) _pages.Add(key, page = new Page());
        var listener = new Listener(onNext, onError);
        page.Listeners.Add(listener);
        var lease = new Lease(() =>
        {
            page.Listeners.Remove(listener);
            if (page.Listeners.Count != 0) return;
            if (_pages.TryGetValue(key, out Page current) && ReferenceEquals(current, page)) _pages.Remove(key);
            page.Dispose();
        });
        try
        {
            if (first)
            {
                IDisposable connection = connect(page.Publish, page.Fail);
                if (page.Disposed) connection.Dispose();
                else page.Connection = connection;
            }
            else if (page.HasValue)
            {
                onNext(page.Value);
            }
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Page[] pages = _pages.Values.ToArray();
        _pages.Clear();
        foreach (Page page in pages) page.Dispose();
    }

    private sealed class Page : IDisposable
    {
        public readonly List<Listener> Listeners = new();
        public IDisposable Connection;
        public bool Disposed;
        public bool HasValue;
        public T Value;

        public void Publish(T value)
        {
            if (Disposed) return;
            Value = value;
            HasValue = true;
            foreach (Listener listener in Listeners.ToArray())
            {
                if (Disposed) break;
                if (!Listeners.Contains(listener)) continue;
                try { listener.Next(value); }
                catch (Exception error) { listener.Error(error); }
            }
        }

        public void Fail(Exception error)
        {
            if (Disposed) return;
            foreach (Listener listener in Listeners.ToArray())
                if (!Disposed && Listeners.Contains(listener)) listener.Error(error);
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Connection?.Dispose();
            Connection = null;
            Listeners.Clear();
            Value = default;
            HasValue = false;
        }
    }

    private sealed class Listener
    {
        public readonly Action<T> Next;
        public readonly Action<Exception> Error;

        public Listener(Action<T> next, Action<Exception> error)
        {
            Next = next;
            Error = error;
        }
    }

    private sealed class Lease : IDisposable
    {
        private Action _release;
        public Lease(Action release) => _release = release;
        public void Dispose()
        {
            Action release = _release;
            _release = null;
            release?.Invoke();
        }
    }
}
