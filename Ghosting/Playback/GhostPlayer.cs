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

public partial class GhostPlayer : IEagerService
{
    private readonly ObjectPool<GhostData> _fullPool;
    private readonly ObjectPool<GhostData> _bulkPool;

    private static ILogger<GhostPlayer> _logger;

    private readonly Dictionary<int, IGhost> _ghosts = new();
    private readonly Dictionary<int, GhostData> _ghostData = new();
    private readonly HashSet<int> _ghostsToRemove = new();
    private readonly BulkGhostRenderService _bulkGhostRenderService;

    private bool _roundStarted;
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

        playerLoopService.SubscribeUpdate(Update);
        playerLoopService.SubscribeFixedUpdate(FixedUpdate);
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
            var bulkCharacterGameObject = new GameObject("Instanced Bulk Character Ghost");
            var bulkArmsUpCharacterGameObject = new GameObject("Instanced Bulk Arms Up Character Ghost");
            var bulkRagdollCharacterGameObject = new GameObject("Instanced Bulk Ragdoll Character Ghost");
            var instancedGhostData = new GhostData(
                gameObject,
                null,
                GhostVisualProfile.Bulk,
                true,
                bulkCharacterGameObject,
                bulkArmsUpCharacterGameObject,
                bulkRagdollCharacterGameObject);
            instancedGhostData.SetBulkCharacterLocalTransform(
                _bulkGhostRenderService.CharacterLocalPosition,
                _bulkGhostRenderService.CharacterLocalRotation);
            return instancedGhostData;
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
        ghostData.SetPlaybackVisible(false);
    }

    private static void ReleaseGhost(GhostData ghostData)
    {
        ghostData.CurrentHorn?.Stop();
        ghostData.CurrentHorn?.Cleanup();
        ghostData.CurrentHorn = null;
        ghostData.ResetRenderState();
        ghostData.SetActive(false);
    }

    private static void DestroyGhost(GhostData ghostData)
    {
        ghostData.DisposeRenderer();
        ghostData.CharacterRig?.Destroy();
        if (ghostData.Visuals != null && ghostData.Visuals.gameObject != null)
            Object.Destroy(ghostData.Visuals.gameObject);
        else if (ghostData.GameObject != null)
            Object.Destroy(ghostData.GameObject);

        if (ghostData.BulkCharacterGameObject != null)
            Object.Destroy(ghostData.BulkCharacterGameObject);
        if (ghostData.BulkArmsUpCharacterGameObject != null)
            Object.Destroy(ghostData.BulkArmsUpCharacterGameObject);
        if (ghostData.BulkRagdollCharacterGameObject != null)
            Object.Destroy(ghostData.BulkRagdollCharacterGameObject);
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
        ghost.Initialize(ghostData);
        if (visualProfile == GhostVisualProfile.Full)
        {
            ghost.ApplyCosmetics(steamName);
            ghostData.InitializeRenderer();
        }

        if (ghostData.IsInstanced)
        {
            _bulkGhostRenderService.Register(ghostData.GameObject.transform);
            _bulkGhostRenderService.RegisterCharacter(ghostData.BulkCharacterGameObject?.transform);
            _bulkGhostRenderService.RegisterArmsUpCharacter(ghostData.BulkArmsUpCharacterGameObject?.transform);
            _bulkGhostRenderService.RegisterRagdollCharacter(ghostData.BulkRagdollCharacterGameObject?.transform);
        }

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
            {
                _bulkGhostRenderService.Unregister(ghostData.GameObject.transform);
                _bulkGhostRenderService.UnregisterCharacter(ghostData.BulkCharacterGameObject?.transform);
                _bulkGhostRenderService.UnregisterArmsUpCharacter(ghostData.BulkArmsUpCharacterGameObject?.transform);
                _bulkGhostRenderService.UnregisterRagdollCharacter(ghostData.BulkRagdollCharacterGameObject?.transform);
            }

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
        if (!_roundStarted)
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
        if (!_roundStarted)
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
