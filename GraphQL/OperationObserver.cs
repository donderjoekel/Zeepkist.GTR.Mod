using System;

namespace TNRD.Zeepkist.GTR.GraphQL;

internal sealed class OperationObserver<T> : IObserver<T>
{
    private readonly Action<T> _onNext;
    private readonly Action<Exception> _onError;
    private readonly Action _onCompleted;

    public OperationObserver(Action<T> onNext, Action<Exception> onError, Action onCompleted = null)
    {
        _onNext = onNext;
        _onError = onError;
        _onCompleted = onCompleted;
    }

    public void OnCompleted()
    {
        _onCompleted?.Invoke();
    }

    public void OnError(Exception error)
    {
        _onError(error);
    }

    public void OnNext(T value)
    {
        _onNext(value);
    }
}
