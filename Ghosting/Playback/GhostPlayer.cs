using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using UnityEngine.Pool;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public partial class GhostPlayer : IEagerService, IDisposable
{
    private readonly ObjectPool<GhostData> _fullPool;
    private readonly ObjectPool<GhostData> _bulkPool;

    private static ILogger<GhostPlayer> _logger;

    private readonly Dictionary<int, IGhost> _ghosts = new();
    private readonly Dictionary<int, GhostData> _ghostData = new();
    private readonly HashSet<int> _ghostsToRemove = new();
    private readonly PlayerLoopService _playerLoopService;
    private readonly BulkGhostRenderService _bulkGhostRenderService;
    private readonly PlayerLoopSubscription _updateSubscription;
    private readonly PlayerLoopSubscription _fixedUpdateSubscription;

    private bool _roundStarted;
    private bool _manualPlaybackActive;
    private bool _paused;

    public IEnumerable<GhostData> ActiveGhosts => _ghostData.Values;

    public event EventHandler<GhostAddedEventArgs> GhostAdded;
    public event EventHandler<GhostRemovedEventArgs> GhostRemoved;

    public GhostPlayer(
        PlayerLoopService playerLoopService,
        BulkGhostRenderService bulkGhostRenderService,
        ILogger<GhostPlayer> logger)
    {
        _logger = logger;
        _playerLoopService = playerLoopService;
        _bulkGhostRenderService = bulkGhostRenderService;
        _fullPool = new ObjectPool<GhostData>(
            CreateFullGhost,
            GetGhost,
            ReleaseGhost,
            DestroyGhost);
        _bulkPool = new ObjectPool<GhostData>(
            CreateBulkGhost,
            GetGhost,
            ReleaseGhost,
            DestroyGhost);

        _updateSubscription = playerLoopService.SubscribeUpdate(Update);
        _fixedUpdateSubscription = playerLoopService.SubscribeFixedUpdate(FixedUpdate);
        RacingApi.RoundStarted += OnRoundStarted;
        RacingApi.RoundEnded += OnRoundEnded;
        RacingApi.PlayerSpawned += OnPlayerSpawned;
        RacingApi.QuickReset += OnQuickReset;
        RacingApi.Quit += OnQuit;
        MultiplayerApi.DisconnectedFromGame += OnDisconnectedFromGame;
    }

    private GhostData CreateFullGhost()
    {
        return CreateVisualGhost(GhostVisualProfile.Full);
    }

    private GhostData CreateBulkGhost()
    {
        if (_bulkGhostRenderService.CanUseInstancing())
        {
            var gameObject = new GameObject("Instanced Bulk Ghost");
            return new GhostData(gameObject, null, GhostVisualProfile.Bulk, true);
        }

        GhostData ghostData = CreateVisualGhost(GhostVisualProfile.Bulk);
        ghostData.InitializeRenderer();
        return ghostData;
    }

    private static GhostData CreateVisualGhost(GhostVisualProfile visualProfile)
    {
        GameObject gameObject = new("Ghost");
        Object.DontDestroyOnLoad(gameObject.transform.root.gameObject);
        GhostVisuals ghostVisuals = gameObject.AddComponent<GhostVisuals>();
        ghostVisuals.Initialize(visualProfile);
        return new GhostData(
            ghostVisuals.GhostModel.gameObject,
            ghostVisuals,
            visualProfile,
            false);
    }

    private static void GetGhost(GhostData ghostData)
    {
        ghostData.SetActive(true);
    }

    private static void ReleaseGhost(GhostData ghostData)
    {
        ghostData.CurrentHorn?.Stop();
        ghostData.CurrentHorn?.Cleanup();
        ghostData.CurrentHorn = null;
        ghostData.ClearIdentity();
        ghostData.SetActive(false);
    }

    private static void DestroyGhost(GhostData ghostData)
    {
        ghostData.DisposeRenderer();
        if (ghostData.Visuals != null && ghostData.Visuals.gameObject != null)
            Object.Destroy(ghostData.Visuals.gameObject);
        else if (ghostData.GameObject != null)
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

    private void OnQuickReset()
    {
        _roundStarted = false;

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

    private void OnQuit()
    {
        _roundStarted = false;
        ClearGhosts();
        ClearPools();
    }

    private void OnDisconnectedFromGame()
    {
        _roundStarted = false;
        ClearGhosts();
        ClearPools();
    }

    public IReadOnlyList<int> GetLoadedGhostIds()
    {
        return _ghosts.Keys.ToList();
    }

    public bool TryGetGhostData(int recordId, out GhostData ghostData)
    {
        return _ghostData.TryGetValue(recordId, out ghostData);
    }

    public IReadOnlyList<LoadedGhostEntry> GetLoadedGhosts()
    {
        return _ghosts.Keys
            .OrderBy(recordId => _ghostData[recordId].DisplayName)
            .Select(recordId => new LoadedGhostEntry(
                recordId,
                _ghostData[recordId].DisplayName,
                _ghostData[recordId],
                _ghosts[recordId]))
            .ToList();
    }

    public bool HasGhost(int recordId)
    {
        return _ghosts.ContainsKey(recordId);
    }

    public bool HasGhost(int recordId, GhostVisualProfile visualProfile)
    {
        return _ghostData.TryGetValue(recordId, out GhostData ghostData) &&
               ghostData.VisualProfile == visualProfile;
    }

    public void AddGhost(
        GhostType type,
        int recordId,
        string steamName,
        IGhost ghost,
        GhostVisualProfile visualProfile = GhostVisualProfile.Full)
    {
        bool hadExistingGhost = false;

        if (_ghostData.TryGetValue(recordId, out GhostData ghostData))
        {
            if (ghostData.VisualProfile == visualProfile)
            {
                hadExistingGhost = true;
                _ghosts[recordId].Stop();
            }
            else
            {
                RemoveGhost(recordId);
                ghostData = GetPool(visualProfile).Get();
            }
        }
        else
        {
            ghostData = GetPool(visualProfile).Get();
        }

        ghostData.Initialize(type, ghost);
        ghostData.SetIdentity(recordId, steamName);
        ghost.Initialize(ghostData);
        if (visualProfile == GhostVisualProfile.Full)
        {
            ghost.ApplyCosmetics(steamName);
            ghostData.InitializeRenderer();
        }

        if (ghostData.IsInstanced)
            _bulkGhostRenderService.Register(ghostData.GameObject.transform);

        if (!hadExistingGhost)
        {
            _ghosts.Add(recordId, ghost);
            _ghostData.Add(recordId, ghostData);
        }
        else
        {
            _ghosts[recordId] = ghost;
        }

        GhostAdded?.Invoke(this, new GhostAddedEventArgs(recordId, ghost, ghostData));
    }

    public void RemoveGhost(int recordId)
    {
        if (!_ghosts.TryGetValue(recordId, out IGhost ghost))
            return;

        ghost.Stop();

        if (_ghostData.TryGetValue(recordId, out GhostData ghostData))
        {
            if (ghostData.IsInstanced)
                _bulkGhostRenderService.Unregister(ghostData.GameObject.transform);

            GetPool(ghostData.VisualProfile).Release(ghostData);
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

    public float GetMaxDuration()
    {
        if (_ghosts.Count == 0)
            return 0f;

        float maxDuration = 0f;
        foreach (IGhost ghost in _ghosts.Values)
        {
            if (ghost.Duration > maxDuration)
                maxDuration = ghost.Duration;
        }

        return maxDuration;
    }

    public void StartManualPlayback()
    {
        _manualPlaybackActive = true;
        _paused = false;

        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Start();
        }
    }

    public void StopManualPlayback()
    {
        _manualPlaybackActive = false;
        _paused = false;

        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Stop();
        }
    }

    public void SeekAllGhosts(float time)
    {
        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Seek(time);
        }
    }

    public void PauseGhosts()
    {
        _paused = true;
    }

    public void ResumeGhosts()
    {
        _paused = false;
    }

    private void Update()
    {
        if (!_roundStarted && !_manualPlaybackActive)
            return;

        if (_paused)
            return;

        _ghostsToRemove.Clear();

        foreach ((int id, IGhost ghost) in _ghosts)
        {
            try
            {
                ghost.Update();
            }
            catch (Exception)
            {
                _ghostsToRemove.Add(id);
            }
        }

        foreach (int id in _ghostsToRemove)
        {
            RemoveGhost(id);
        }
    }

    private void FixedUpdate()
    {
        if (!_roundStarted && !_manualPlaybackActive)
            return;

        if (_paused)
            return;

        _ghostsToRemove.Clear();

        foreach ((int id, IGhost ghost) in _ghosts)
        {
            try
            {
                ghost.FixedUpdate();
            }
            catch (Exception)
            {
                _ghostsToRemove.Add(id);
            }
        }

        foreach (int id in _ghostsToRemove)
        {
            RemoveGhost(id);
        }
    }

    public void Dispose()
    {
        _playerLoopService.UnsubscribeUpdate(_updateSubscription);
        _playerLoopService.UnsubscribeFixedUpdate(_fixedUpdateSubscription);
        RacingApi.RoundStarted -= OnRoundStarted;
        RacingApi.RoundEnded -= OnRoundEnded;
        RacingApi.PlayerSpawned -= OnPlayerSpawned;
        RacingApi.QuickReset -= OnQuickReset;
        RacingApi.Quit -= OnQuit;
        MultiplayerApi.DisconnectedFromGame -= OnDisconnectedFromGame;
        ClearGhosts();
        ClearPools();
    }

    private ObjectPool<GhostData> GetPool(GhostVisualProfile visualProfile)
    {
        return visualProfile == GhostVisualProfile.Bulk ? _bulkPool : _fullPool;
    }

    private void ClearPools()
    {
        _fullPool.Clear();
        _bulkPool.Clear();
        GhostRenderer.DisposeSharedResources();
    }
}
