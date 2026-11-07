using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Utilities;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostEnablementTests
{
    private sealed class Settings
    {
        public bool Enabled = true;
        public event EventHandler Changed;
        public void Set(bool value) { Enabled = value; Changed?.Invoke(this, EventArgs.Empty); }
        public void Subscribe(EventHandler handler) => Changed += handler;
        public void Unsubscribe(EventHandler handler) => Changed -= handler;
    }

    [Fact]
    public void Disable_CancelsBothLoadsBeforeStoppingAndClearing()
    {
        var settings = new Settings();
        var calls = new List<string>();
        using var service = new GhostEnablementService(() => settings.Enabled, settings.Subscribe, settings.Unsubscribe,
            action => action(), () => { calls.Add("cancel online"); calls.Add("cancel offline"); },
            () => { calls.Add("stop"); calls.Add("clear"); }, () => calls.Add("reload"));
        settings.Set(false);
        Assert.Equal(new[] { "cancel online", "cancel offline", "stop", "clear" }, calls);
    }

    [Fact]
    public async Task Disable_CancelsDownloadAndDiscardsQueuedCreation()
    {
        var settings = new Settings();
        using var load = new CancellationTokenSource();
        using var queue = new BoundedMainThreadQueue<Action>(4);
        bool created = false;
        Task download = Task.Delay(Timeout.Infinite, load.Token);
        await queue.EnqueueAsync(() => created = true, load.Token);
        using var service = new GhostEnablementService(() => settings.Enabled, settings.Subscribe, settings.Unsubscribe,
            action => action(), () => { load.Cancel(); queue.DiscardCancelled(); }, () => { }, () => { });
        settings.Set(false);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download);
        Assert.False(queue.TryDequeue(out _));
        Assert.False(created);
    }

    [Fact]
    public void QueuedSettingChanges_ReadLatestValueAndCancelStaleLoads()
    {
        var settings = new Settings();
        var mainThread = new Queue<Action>();
        int cancellations = 0, clears = 0, reloads = 0;
        using var service = new GhostEnablementService(() => settings.Enabled, settings.Subscribe, settings.Unsubscribe,
            mainThread.Enqueue, () => cancellations++, () => clears++, () => reloads++);
        settings.Set(false);
        settings.Set(true);
        Assert.Equal(0, cancellations);
        while (mainThread.TryDequeue(out var action)) action();
        Assert.Equal(2, cancellations);
        Assert.Equal(0, clears);
        Assert.Equal(2, reloads);
        settings.Set(false);
        mainThread.Dequeue()();
        Assert.Equal(1, clears);
    }

    [Fact]
    public void Dispose_UnsubscribesAndSuppressesAlreadyQueuedChanges()
    {
        var settings = new Settings();
        var mainThread = new Queue<Action>();
        var service = new GhostEnablementService(() => settings.Enabled, settings.Subscribe, settings.Unsubscribe,
            mainThread.Enqueue, () => throw new Exception("disposed service handled change"), () => { }, () => { });
        settings.Set(false);
        service.Dispose();
        service.Dispose();
        mainThread.Dequeue()();
        settings.Set(true);
        Assert.Empty(mainThread);
    }
}
