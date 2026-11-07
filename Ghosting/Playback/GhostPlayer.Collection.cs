using System;
using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public partial class GhostPlayer
{
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

    public void RemoveGhost(int recordId)
    {
        if (!_ghosts.TryGetValue(recordId, out IGhost ghost))
            return;

        ghost.Stop(_timingService.CurrentTime);

        _ghostData.TryGetValue(recordId, out GhostData ghostData);
        if (ghostData?.IsInstanced == true)
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

        _ghostData.Remove(recordId);
        _ghosts.Remove(recordId);
        if (ghostData != null)
            GetPool(ghostData.VisualProfile).Release(ghostData);
        GhostRemoved?.Invoke(this, new GhostRemovedEventArgs(recordId));
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
}
