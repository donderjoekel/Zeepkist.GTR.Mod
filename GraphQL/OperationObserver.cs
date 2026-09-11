using System;

namespace TNRD.Zeepkist.GTR.GraphQL;

internal sealed class OperationObserver<T> : IObserver<T>
{
    private readonly Action<T> _onNext;
    private readonly Action<Exception> _onError;

    public OperationObserver(Action<T> onNext, Action<Exception> onError)
    {
        _onNext = onNext;
        _onError = onError;
    }

    public void OnCompleted()
    {
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
