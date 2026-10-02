using System;
using System.Threading;
using System.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.GraphQL;

/// <summary>Owns one subscription at a time. Completion also means reconnect for live queries.</summary>
internal sealed class RecoveringSubscription<T> : IDisposable
{
    private readonly Func<IObserver<T>, IDisposable> _subscribe;
    private readonly Action<T, int> _onNext;
    private readonly Action<Exception> _onError;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<double> _jitter;
    private readonly CancellationTokenSource _cancellation = new();
    private int _attempt;
    private int _failures;
    private int _started;
    private readonly object _lifetime = new();
    private bool _disposed;
    private bool _finished;

    public RecoveringSubscription(
        Func<IObserver<T>, IDisposable> subscribe,
        Action<T, int> onNext,
        Action<Exception> onError,
        Func<TimeSpan, CancellationToken, Task> delay = null,
        Func<double> jitter = null)
    {
        _subscribe = subscribe;
        _onNext = onNext;
        _onError = onError;
        _delay = delay ?? Task.Delay;
        var random = new Random();
        _jitter = jitter ?? random.NextDouble;
    }

    public Task Completion { get; private set; } = Task.CompletedTask;

    public void Start()
    {
        lock (_lifetime)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RecoveringSubscription<T>));
            if (Interlocked.Exchange(ref _started, 1) == 0)
                Completion = Task.Run(RunAsync);
        }
    }

    public bool IsCurrentAttempt(int attempt) =>
        !_cancellation.IsCancellationRequested && attempt == Volatile.Read(ref _attempt);

    public void MarkHealthy(int attempt)
    {
        if (IsCurrentAttempt(attempt))
            Interlocked.Exchange(ref _failures, 0);
    }

    internal static TimeSpan RetryDelay(int failures, double jitter) =>
        TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(5, failures))) * (0.8 + jitter * 0.4));

    private async Task RunAsync()
    {
        CancellationToken token = _cancellation.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                int attempt = Interlocked.Increment(ref _attempt);
                var ended = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (token.Register(() => ended.TrySetCanceled()))
                {
                    IDisposable subscription = null;
                    try
                    {
                        void Finish(Exception error)
                        {
                            if (Interlocked.CompareExchange(ref _attempt, attempt + 1, attempt) == attempt)
                                ended.TrySetResult(error);
                        }
                        subscription = _subscribe(new OperationObserver<T>(
                            value =>
                            {
                                if (IsCurrentAttempt(attempt) && !ended.Task.IsCompleted)
                                    _onNext(value, attempt);
                            },
                            Finish,
                            () => Finish(null)));
                        Exception error = await ended.Task.ConfigureAwait(false);
                        if (error != null && !token.IsCancellationRequested)
                            _onError(error);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception error)
                    {
                        if (!token.IsCancellationRequested)
                            _onError(error);
                    }
                    finally
                    {
                        // Invalidate queued callbacks before disposing the transport.
                        Interlocked.Increment(ref _attempt);
                        try { subscription?.Dispose(); }
                        catch (Exception error)
                        {
                            if (!token.IsCancellationRequested)
                                _onError(error);
                        }
                    }
                }

                token.ThrowIfCancellationRequested();
                int failures = Interlocked.Increment(ref _failures) - 1;
                await _delay(RetryDelay(failures, _jitter()), token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_lifetime)
            {
                _finished = true;
                _cancellation.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (_lifetime)
        {
            if (_disposed) return;
            _disposed = true;
            if (!_finished)
                _cancellation.Cancel();
            if (_started == 0)
            {
                _finished = true;
                _cancellation.Dispose();
            }
        }
    }
}
