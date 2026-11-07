using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.PlayerLoop;
using ZeepSDK.PhotoMode;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

[Collection("Playback lifecycle")]
public class GhostRemovalTests
{
    private sealed class Ghost(float duration) : IGhost
    {
        public float Duration => duration;
        public bool Stopped { get; private set; }
        public void Stop(float time) { Assert.Equal(12, time); Stopped = true; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Removal_CompletesBeforePoolReleaseAndNotification(bool instanced)
    {
        var player = new GhostPlayer();
        var ghost = new Ghost(20);
        var data = new GhostData { IsInstanced = instanced };
        player.AddForTest(1, ghost, data);
        bool released = false;
        player.Pool.OnRelease = value =>
        {
            Assert.True(ghost.Stopped);
            Assert.False(player.HasGhost(1));
            Assert.False(player.TryGetGhostData(1, out _));
            released = true;
        };
        int events = 0;
        player.GhostRemoved += (sender, args) =>
        {
            events++;
            Assert.Equal(1, args.RecordId);
            Assert.True(released);
            Assert.False(player.HasGhost(1));
            Assert.Empty(player.GetLoadedGhostIds());
            Assert.Empty(player.GetLoadedGhosts());
        };
        player.RemoveGhost(1);
        player.RemoveGhost(1);
        player.RemoveGhost(999);
        Assert.Equal(1, events);
        Assert.Same(data, player.Pool.Get());
    }

    [Fact]
    public void Subscribers_SeeRemainingDurationsAndCameraEntries()
    {
        var player = new GhostPlayer();
        player.AddForTest(1, new Ghost(30), new GhostData());
        player.AddForTest(2, new Ghost(10), new GhostData());
        float duration = -1;
        IReadOnlyList<LoadedGhostEntry> targets = null;
        player.GhostRemoved += (_, _) =>
        {
            duration = player.GetMaxDuration();
            targets = player.GetLoadedGhosts();
        };
        player.RemoveGhost(1);
        Assert.Equal(10, duration);
        Assert.Equal(2, Assert.Single(targets).RecordId);
        player.RemoveGhost(2);
        Assert.Equal(0, duration);
        Assert.Empty(targets);
    }

    [Fact]
    public void ClearGhosts_PublishesConsistentRemainingSets()
    {
        var player = new GhostPlayer();
        for (int id = 1; id <= 3; id++) player.AddForTest(id, new Ghost(id), new GhostData());
        var counts = new List<int>();
        player.GhostRemoved += (sender, args) =>
        {
            Assert.False(player.TryGetGhostData(args.RecordId, out _));
            Assert.DoesNotContain(args.RecordId, player.GetLoadedGhostIds());
            counts.Add(player.GetLoadedGhosts().Count);
        };
        player.ClearGhosts();
        Assert.Equal(new[] { 2, 1, 0 }, counts);
        Assert.Equal(0, player.GetMaxDuration());
    }

    [Fact]
    public void LastRemoval_UpdatesPlaybackDurationAndRestoresNativeCameraTargets()
    {
        var player = new GhostPlayer();
        player.AddForTest(1, new Ghost(30), new GhostData());
        var playback = new GhostPlaybackService(new GhostTimingService(), player, new PlayerLoopService());
        var photoMode = new PhotoModeTimelineService(player, playback);
        using var targets = new GtrSpectateTargetService(player, photoMode);
        var camera = new FlyingCameraScript();
        var nativeTarget = new SpectatorZeepkistTarget { transform = new UnityEngine.Transform(), name = "native" };
        camera.RebuildTargets = () =>
        {
            if (targets.ShouldInjectGhosts) targets.ApplyGhostTargets(camera);
            else
            {
                camera.targetList.Clear();
                camera.targetList.Add(nativeTarget);
                camera.currentTarget = nativeTarget;
            }
        };
        PhotoModeFlyingCamera.Current = camera;
        try
        {
            PhotoModeApi.Enter();
            var ghostTarget = Assert.Single(camera.targetList);
            Assert.True(targets.TryGetRecordId(ghostTarget.transform, out _));
            Assert.Equal(30, playback.Duration);
            player.RemoveGhost(1);
            Assert.Equal(0, playback.Duration);
            Assert.Same(nativeTarget, Assert.Single(camera.targetList));
            Assert.Same(nativeTarget, camera.currentTarget);
            Assert.False(targets.TryGetRecordId(ghostTarget.transform, out _));
        }
        finally
        {
            PhotoModeFlyingCamera.Current = null;
            PhotoModeApi.Exit();
        }
    }
}
