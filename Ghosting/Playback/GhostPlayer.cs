using System;
using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using UnityEngine.Pool;
using ZeepSDK.Racing;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public partial class GhostPlayer : IEagerService
{
    private readonly ObjectPool<GhostData> _pool = new(CreateGhost, GetGhost, ReleaseGhost, DestroyGhost);

    private readonly Dictionary<int, IGhost> _ghosts = new();
    private readonly Dictionary<int, GhostData> _ghostData = new();

    private bool _roundStarted;

    public IEnumerable<GhostData> ActiveGhosts => _ghostData.Values;

    public event EventHandler<GhostAddedEventArgs> GhostAdded;
    public event EventHandler<GhostRemovedEventArgs> GhostRemoved;

    public GhostPlayer(PlayerLoopService playerLoopService)
    {
        playerLoopService.SubscribeUpdate(Update);
        playerLoopService.SubscribeFixedUpdate(FixedUpdate);
        RacingApi.RoundStarted += OnRoundStarted;
        RacingApi.RoundEnded += OnRoundEnded;
        RacingApi.PlayerSpawned += OnPlayerSpawned;
    }

    private static GhostData CreateGhost()
    {
        GameObject gameObject = new("Ghost");
        GhostVisuals ghostVisuals = gameObject.AddComponent<GhostVisuals>();
        return new GhostData(ghostVisuals);
    }

    private static void GetGhost(GhostData ghostData)
    {
        ghostData.GameObject.SetActive(true);
    }

    private static void ReleaseGhost(GhostData ghostData)
    {
        ghostData.GameObject.SetActive(false);
    }

    private static void DestroyGhost(GhostData ghostData)
    {
        Object.Destroy(ghostData.GameObject);
    }

    private void OnRoundStarted()
    {
        _roundStarted = true;

        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Start();
        }
    }

    private void OnRoundEnded()
    {
        _roundStarted = false;

        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Stop();
        }
    }

    private void OnPlayerSpawned()
    {
        _roundStarted = false;

        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Stop();
        }
    }

    public IReadOnlyList<int> GetLoadedGhostIds()
    {
        return _ghosts.Keys.ToList();
    }

    public bool HasGhost(int recordId)
    {
        return _ghosts.ContainsKey(recordId);
    }

    public void AddGhost(int recordId, string steamName, IGhost ghost)
    {
        if (HasGhost(recordId))
            return;

        // The order here is important. The renderer needs to be the last thing to make sure we got all the materials etc
        GhostData ghostData = _pool.Get();
        ghostData.Initialize(ghost);
        ghost.Initialize(ghostData);
        ghost.ApplyCosmetics(steamName);
        ghostData.InitializeRenderer();

        _ghosts.Add(recordId, ghost);
        _ghostData.Add(recordId, ghostData);

        GhostAdded?.Invoke(this, new GhostAddedEventArgs(recordId, ghost, ghostData));
    }

    public void RemoveGhost(int recordId)
    {
        if (!_ghosts.TryGetValue(recordId, out IGhost ghost))
            return;

        ghost.Stop();

        if (_ghostData.TryGetValue(recordId, out GhostData ghostData))
        {
            _pool.Release(ghostData);
            GhostRemoved?.Invoke(this, new GhostRemovedEventArgs(recordId));
        }

        _ghostData.Remove(recordId);
        _ghosts.Remove(recordId);
    }

    public void ClearGhosts()
    {
        List<int> recordIds = _ghosts.Keys.ToList();
        foreach (int recordId in recordIds)
        {
            RemoveGhost(recordId);
        }
    }

    private void Update()
    {
        if (!_roundStarted)
            return;

        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Update();
        }
    }

    private void FixedUpdate()
    {
        if (!_roundStarted)
            return;

        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.FixedUpdate();
        }
    }
}
