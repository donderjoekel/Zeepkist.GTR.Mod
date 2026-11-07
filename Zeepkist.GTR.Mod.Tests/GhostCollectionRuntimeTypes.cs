// Native rendering/pool adapters. Collection queries and removal run production source unchanged.
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public sealed class GhostData
{
    public string DisplayName { get; set; } = "ghost";
    public bool IsInstanced { get; set; }
    public GhostVisualProfile VisualProfile { get; set; }
    public TestVisuals Visuals { get; set; }
    public GameObject GameObject { get; } = new();
    public GameObject BulkCharacterGameObject { get; } = new();
    public GameObject BulkArmsUpCharacterGameObject { get; } = new();
    public GameObject BulkRagdollCharacterGameObject { get; } = new();
    public sealed class TestVisuals { public GameObject GhostModel { get; } = new(); }
}

public partial class GhostPlayer
{
    private readonly Dictionary<int, IGhost> _ghosts = new();
    private readonly Dictionary<int, GhostData> _ghostData = new();
    private readonly TestTiming _timingService = new();
    private readonly TestRenderer _bulkGhostRenderService = new();
    internal readonly TestPool Pool = new();
    public event EventHandler<GhostRemovedEventArgs> GhostRemoved;

    internal void AddForTest(int id, IGhost ghost, GhostData data)
    {
        _ghosts.Add(id, ghost);
        _ghostData.Add(id, data);
        GhostAdded?.Invoke(this, new GhostAddedEventArgs(id, ghost, data));
    }
    private TestPool GetPool(GhostVisualProfile profile) => Pool;
    private sealed class TestTiming { public float CurrentTime => 12; }
    private sealed class TestRenderer
    {
        public void Unregister(Transform transform) { }
        public void UnregisterCharacter(Transform transform, GhostCharacterPlaybackPose pose) { }
    }
    internal sealed class TestPool
    {
        private readonly Stack<GhostData> _available = new();
        internal Action<GhostData> OnRelease;
        public void Release(GhostData data) { OnRelease?.Invoke(data); data.DisplayName = null; _available.Push(data); }
        internal GhostData Get() => _available.Pop();
    }
}
