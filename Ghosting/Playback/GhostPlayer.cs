using System;
using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using UnityEngine.Pool;
using ZeepSDK.Racing;
using ZeepSDK.Utilities;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public partial class GhostPlayer : IEagerService
{
    private readonly ObjectPool<GhostVisuals> _pool = new(CreateGhost, GetGhost, ReleaseGhost, DestroyGhost);

    private readonly Dictionary<int, IGhost> _ghosts = new();
    private readonly Dictionary<int, GhostVisuals> _ghostVisuals = new();

    private bool _roundStarted;

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

    private static GhostVisuals CreateGhost()
    {
        GameObject gameObject = new("Ghost");
        GhostVisuals ghostVisuals = gameObject.AddComponent<GhostVisuals>();
        return ghostVisuals;
    }

    private static void GetGhost(GhostVisuals obj)
    {
        obj.gameObject.SetActive(true);
    }

    private static void ReleaseGhost(GhostVisuals obj)
    {
        obj.gameObject.SetActive(false);
    }

    private static void DestroyGhost(GhostVisuals obj)
    {
        Object.Destroy(obj.gameObject);
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

    public bool HasGhost(int recordId)
    {
        return _ghosts.ContainsKey(recordId);
    }

    public void AddGhost(int recordId, string steamName, IGhost ghost)
    {
        if (HasGhost(recordId))
            return;

        _ghosts.Add(recordId, ghost);
        GhostVisuals networkedZeepkistGhost = _pool.Get();
        _ghostVisuals.Add(recordId, networkedZeepkistGhost);
        ghost.Initialize(networkedZeepkistGhost);
        ghost.ApplyCosmetics(steamName);

        GhostAdded?.Invoke(this, new GhostAddedEventArgs(recordId, ghost, networkedZeepkistGhost));
    }

    public void ClearGhosts()
    {
        foreach ((int recordId, IGhost _) in _ghosts)
        {
            if (_ghostVisuals.TryGetValue(recordId, out GhostVisuals ghostVisuals))
            {
                _pool.Release(ghostVisuals);
                GhostRemoved?.Invoke(this, new GhostRemovedEventArgs(recordId));
            }
        }

        _ghostVisuals.Clear();
        _ghosts.Clear();
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
