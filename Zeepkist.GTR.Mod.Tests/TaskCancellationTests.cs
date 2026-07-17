using TNRD.Zeepkist.GTR.Utilities;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class TaskCancellationTests
{
    [Fact]
    public async Task CallerCancellationDoesNotCancelSharedTask()
    {
        TaskCompletionSource<int> shared = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource caller = new();

        Task<int> wait = TaskCancellation.WaitAsync(shared.Task, caller.Token);
        caller.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => wait);
        Assert.False(shared.Task.IsCanceled);

        shared.SetResult(42);
        Assert.Equal(42, await shared.Task);
    }

    [Fact]
    public async Task CompletedSharedTaskReturnsResult()
    {
        Assert.Equal(42, await TaskCancellation.WaitAsync(Task.FromResult(42), CancellationToken.None));
    }
}
