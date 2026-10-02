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
    private readonly BulkGhostModeState _bulkModeState;
    private readonly GhostTimingService _timingService;

    private bool _roundStarted;
    private bool _manualPlaybackActive;
    private bool _paused;

    public IEnumerable<GhostData> ActiveGhosts => _ghostData.Values;

    public event EventHandler<GhostAddedEventArgs> GhostAdded;
    public event EventHandler<GhostRemovedEventArgs> GhostRemoved;

    public GhostPlayer(
        PlayerLoopService playerLoopService,
        BulkGhostRenderService bulkGhostRenderService,
        BulkGhostModeState bulkModeState,
        GhostTimingService timingService,
        ILogger<GhostPlayer> logger)
    {
        _logger = logger;
        _bulkGhostRenderService = bulkGhostRenderService;
        _bulkModeState = bulkModeState;
        _timingService = timingService;
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
            instancedGhostData.SetBulkRagdollRotationOffset(_bulkGhostRenderService.RagdollRotationOffset);
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
        ghostData.ResetPlaybackState();
        ghostData.SetActive(true);
    }

    private static void ReleaseGhost(GhostData ghostData)
    {
        ghostData.CurrentHorn?.Stop();
        ghostData.CurrentHorn?.Cleanup();
        ghostData.CurrentHorn = null;
        ghostData.ClearIdentity();
        ghostData.ResetPlaybackState();
        ghostData.SetActive(false);
    }

    private static void DestroyGhost(GhostData ghostData)
    {
        ghostData.DisposeRenderer();
        ghostData.ClearCharacterRig(false);
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
        if (_manualPlaybackActive)
            return;

        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Start(_timingService.CurrentTime);
        }
    }

    private void OnQuickReset()
    {
        _roundStarted = false;
        if (_manualPlaybackActive)
            return;

        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Start(_timingService.CurrentTime);
        }
    }

    private void OnRoundEnded()
    {
        _roundStarted = false;
        if (_manualPlaybackActive)
            return;

        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Stop(_timingService.CurrentTime);
        }
    }

    private void OnPlayerSpawned()
    {
        _roundStarted = false;
        if (_manualPlaybackActive)
            return;

        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Stop(_timingService.CurrentTime);
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
            .Select(recordId => new LoadedGhostEntry(
                recordId,
                _ghostData[recordId].DisplayName,
                _ghosts[recordId].Duration,
                _ghostData[recordId],
                _ghosts[recordId]))
            .OrderBy(entry => entry.Duration)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
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
                _ghosts[recordId].Stop(_timingService.CurrentTime);
                ghostData.ClearIdentity();
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
        ghost.Initialize(ghostData, _bulkModeState, _timingService);
        if (visualProfile == GhostVisualProfile.Full)
        {
            ghostData.PrepareForCosmeticsReuse();
            ghost.ApplyCosmetics(steamName);
            ghostData.InitializeRenderer();
        }

        if (ghostData.IsInstanced)
        {
            _bulkGhostRenderService.Register(ghostData.GameObject.transform);
            _bulkGhostRenderService.RegisterCharacter(
                ghostData.BulkCharacterGameObject?.transform,
                GhostCharacterPlaybackPose.Seated,
                ghost.Color);
            _bulkGhostRenderService.RegisterCharacter(
                ghostData.BulkArmsUpCharacterGameObject?.transform,
                GhostCharacterPlaybackPose.SeatedArmsUp,
                ghost.Color);
            _bulkGhostRenderService.RegisterCharacter(
                ghostData.BulkRagdollCharacterGameObject?.transform,
                GhostCharacterPlaybackPose.Ragdoll,
                ghost.Color);
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

        if (_roundStarted || _manualPlaybackActive)
        {
            float currentTime = _timingService.CurrentTime;
            ghost.Start(currentTime);
            if (_paused)
                ghost.Pause(currentTime);
        }

        GhostAdded?.Invoke(this, new GhostAddedEventArgs(recordId, ghost, ghostData));
    }

    public void RemoveGhost(int recordId)
    {
        if (!_ghosts.TryGetValue(recordId, out IGhost ghost))
            return;

        ghost.Stop(_timingService.CurrentTime);

        if (_ghostData.TryGetValue(recordId, out GhostData ghostData))
        {
            if (ghostData.IsInstanced)
            {
                _bulkGhostRenderService.Unregister(ghostData.GameObject.transform);
                _bulkGhostRenderService.UnregisterCharacter(
                    ghostData.BulkCharacterGameObject?.transform,
                    GhostCharacterPlaybackPose.Seated);
                _bulkGhostRenderService.UnregisterCharacter(
                    ghostData.BulkArmsUpCharacterGameObject?.transform,
                    GhostCharacterPlaybackPose.SeatedArmsUp);
                _bulkGhostRenderService.UnregisterCharacter(
                    ghostData.BulkRagdollCharacterGameObject?.transform,
                    GhostCharacterPlaybackPose.Ragdoll);
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
        if (_paused)
            return;

        _paused = true;
        float currentTime = _timingService.CurrentTime;
        foreach (IGhost ghost in _ghosts.Values)
            ghost.Pause(currentTime);
    }

    public void ResumeGhosts()
    {
        if (!_paused)
            return;

        _paused = false;
        float currentTime = _timingService.CurrentTime;
        foreach (IGhost ghost in _ghosts.Values)
            ghost.Resume(currentTime);
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

    public bool TryStepFrame(float currentTime, int direction, float timeEpsilon, out float newTime)
    {
        newTime = currentTime;
        if (_ghosts.Count == 0)
            return false;

        GhostBase referenceGhost = null;
        float maxDuration = 0f;
        foreach (IGhost ghost in _ghosts.Values)
        {
            if (ghost.Duration <= maxDuration || ghost is not GhostBase ghostBase)
                continue;

            maxDuration = ghost.Duration;
            referenceGhost = ghostBase;
        }

        if (referenceGhost == null)
            return false;

        return referenceGhost.TryGetAdjacentFrameTime(currentTime, direction, timeEpsilon, out newTime);
    }

    public void StartManualPlayback()
    {
        _manualPlaybackActive = true;
        _paused = false;

        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Start(_timingService.CurrentTime);
        }
    }

    public void StopManualPlayback()
    {
        _manualPlaybackActive = false;
        _paused = false;

        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Stop(_timingService.CurrentTime);
        }
    }

    public void SeekAllGhosts(float time)
    {
        foreach ((int _, IGhost ghost) in _ghosts)
        {
            ghost.Seek(time);
        }
    }

    internal void RestartRoundPlayback()
    {
        if (!_roundStarted)
            return;

        float currentTime = _timingService.CurrentTime;
        foreach (IGhost ghost in _ghosts.Values)
            ghost.Start(currentTime);
    }

    private void Update()
    {
        _timingService.Advance();

        if (!_roundStarted && !_manualPlaybackActive)
            return;

        if (_paused)
            return;

        float currentTime = _timingService.CurrentTime;
        _ghostsToRemove.Clear();

        foreach ((int id, IGhost ghost) in _ghosts)
        {
            try
            {
                ghost.Sample(currentTime);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Removing ghost {RecordId} after sampling failed", id);
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
