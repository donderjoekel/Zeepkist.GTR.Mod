using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Utilities;

/// <summary>FIFO session queues. One producer worker and one consumer worker.</summary>
internal sealed class SerialPipeline<TInput, TOutput> : IDisposable
{
    private sealed class Fifo<T>
    {
        private readonly Queue<T> _items = new();
        private readonly SemaphoreSlim _available = new(0);
        public void Add(T item) { lock (_items) { _items.Enqueue(item); _available.Release(); } }
        public async Task<T> Take(CancellationToken token)
        {
            await _available.WaitAsync(token).ConfigureAwait(false);
            lock (_items) return _items.Dequeue();
        }
        public void Clear()
        {
            lock (_items)
            {
                while (_items.Count > 0)
                    if (_items.Dequeue() is IDisposable disposable)
                        disposable.Dispose();
            }
        }
    }

    private readonly Fifo<TInput> _input = new();
    private readonly Fifo<TOutput> _output = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Func<TInput, CancellationToken, Task<TOutput>> _encode;
    private readonly Func<TOutput, CancellationToken, Task> _upload;
    private readonly Action<Exception> _onError;
    private readonly object _gate = new();
    private bool _disposed;

    public SerialPipeline(
        Func<TInput, CancellationToken, Task<TOutput>> encode,
        Func<TOutput, CancellationToken, Task> upload,
        Action<Exception> onError)
    {
        _encode = encode;
        _upload = upload;
        _onError = onError;
        Completion = Task.WhenAll(Task.Run(EncodeLoop), Task.Run(UploadLoop));
    }

    public Task Completion { get; }

    public void Enqueue(TInput input)
    {
        lock (_gate)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SerialPipeline<TInput, TOutput>));
            _input.Add(input);
        }
    }

    private async Task EncodeLoop()
    {
        CancellationToken token = _cancellation.Token;
        try
        {
            while (true)
            {
                TInput input = await _input.Take(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    TOutput output = await _encode(input, token).ConfigureAwait(false);
                    lock (_gate)
                    {
                        if (_disposed)
                        {
                            if (output is IDisposable disposable) disposable.Dispose();
                        }
                        else _output.Add(output);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception error) { _onError(error); }
                finally { if (input is IDisposable disposable) disposable.Dispose(); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { _input.Clear(); }
    }

    private async Task UploadLoop()
    {
        CancellationToken token = _cancellation.Token;
        try
        {
            while (true)
            {
                TOutput output = await _output.Take(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    await _upload(output, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception error) { _onError(error); }
                finally { if (output is IDisposable disposable) disposable.Dispose(); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { _output.Clear(); }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _cancellation.Cancel();
            // Each worker releases its queue after its acquired reads have unwound.
            _ = Completion.ContinueWith(_ => _cancellation.Dispose(), TaskScheduler.Default);
        }
    }
}
