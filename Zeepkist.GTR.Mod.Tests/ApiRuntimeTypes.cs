// Headless adapters for Steam identity and UniTask. HTTP/authentication code itself is linked unchanged.
using System.Runtime.CompilerServices;

namespace Steamworks
{
    public sealed class AuthTicket : IDisposable
    {
        public byte[] Data => [0x12, 0x34];
        public void Dispose() { }
    }
    public static class SteamUser
    {
        public static AuthTicket GetAuthSessionTicket(Data.NetIdentity identity) => new();
    }
    public static class SteamClient
    {
        public static bool IsValid { get; set; } = true;
        public static bool IsLoggedOn { get; set; } = true;
        public static ulong SteamId => 42;
    }
}
namespace Steamworks.Data { public struct NetIdentity; }

namespace TNRD.Zeepkist.GTR
{
    public static class MyPluginInfo { public const string PLUGIN_VERSION = "test"; }
}
namespace TNRD.Zeepkist.GTR.Connectivity
{
    public sealed class SpainRoutingService
    {
        public Task GetTraceResultAsync() => Task.CompletedTask;
    }
}

namespace ZeepSDK.External.Cysharp.Threading.Tasks
{
    public static class UniTask
    {
        // Native main-thread scheduling requires in-game validation.
        public static Task SwitchToMainThread(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
    [AsyncMethodBuilder(typeof(UniTaskBuilder<>))]
    public readonly struct UniTask<T>(Task<T> task)
    {
        public TaskAwaiter<T> GetAwaiter() => task.GetAwaiter();
        public Task<T> AsTask() => task;
    }
    public struct UniTaskBuilder<T>
    {
        private AsyncTaskMethodBuilder<T> _builder;
        public static UniTaskBuilder<T> Create() => new() { _builder = AsyncTaskMethodBuilder<T>.Create() };
        public UniTask<T> Task => new(_builder.Task);
        public void SetResult(T result) => _builder.SetResult(result);
        public void SetException(Exception error) => _builder.SetException(error);
        public void SetStateMachine(IAsyncStateMachine stateMachine) => _builder.SetStateMachine(stateMachine);
        public void Start<TState>(ref TState stateMachine) where TState : IAsyncStateMachine => _builder.Start(ref stateMachine);
        public void AwaitOnCompleted<TAwaiter, TState>(ref TAwaiter awaiter, ref TState stateMachine)
            where TAwaiter : INotifyCompletion where TState : IAsyncStateMachine =>
            _builder.AwaitOnCompleted(ref awaiter, ref stateMachine);
        public void AwaitUnsafeOnCompleted<TAwaiter, TState>(ref TAwaiter awaiter, ref TState stateMachine)
            where TAwaiter : ICriticalNotifyCompletion where TState : IAsyncStateMachine =>
            _builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
    }
}
