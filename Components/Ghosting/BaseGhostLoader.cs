using System;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using UnityEngine;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting;

public abstract class BaseGhostLoader : MonoBehaviourWithLogging
{
    private int validIdentifier;

    protected override void Awake()
    {
        base.Awake();
        RacingApi.PlayerSpawned += OnSpawnPlayers;
    }

    protected virtual void OnDestroy()
    {
        RacingApi.PlayerSpawned -= OnSpawnPlayers;
    }

    private void OnSpawnPlayers()
    {
        if (!Plugin.ConfigEnableGhosts.Value)
            return;

        ClearGhosts();
        LoadGhosts(++validIdentifier).Forget();
    }

    protected abstract bool ContainsGhost(MediaResponseModel mediaModel);
    protected abstract void AddGhost(MediaResponseModel mediaModel, GameObject ghost);
    protected abstract void ClearGhosts();
    protected abstract UniTaskVoid LoadGhosts(int identifier);

    protected void SpawnWorldRecord(
        int identifier,
        MediaResponseModel mediaModel,
        RecordResponseModel recordModel,
        UserResponseModel userModel
    )
    {
        if (mediaModel == null)
            return;

        string holderName = string.IsNullOrEmpty(userModel.SteamName)
            ? userModel.SteamId
            : userModel.SteamName;
        SpawnGhost(identifier,
            mediaModel,
            userModel,
            $"WR\n{holderName}\r\n{recordModel.Time.GetFormattedTime()}",
            new Color(0.6f, 0, 0.8f));
    }

    protected void SpawnPersonalBest(
        int identifier,
        MediaResponseModel mediaModel,
        RecordResponseModel recordModel,
        UserResponseModel userModel
    )
    {
        if (mediaModel == null)
            return;

        if (string.IsNullOrEmpty(mediaModel.GhostUrl))
        {
            Logger.LogInfo("Skipping ghost because there's no ghost url yet");
            return;
        }

        SpawnGhost(identifier,
            mediaModel,
            userModel,
            $"PB\n{recordModel.Time.GetFormattedTime()}",
            new Color(0, 0.7f, 0));
    }

    protected void SpawnGhost(
        int identifier,
        MediaResponseModel mediaModel,
        UserResponseModel userModel,
        string name,
        Color? color
    )
    {
        if (ContainsGhost(mediaModel))
            return;

        if (identifier != validIdentifier)
            return;

        GameObject ghost = new($"Ghost for {name}");
        GhostPlayer ghostPlayer = ghost.AddComponent<GhostPlayer>();
        GhostVisuals ghostVisuals = ghost.AddComponent<GhostVisuals>();

        ghostPlayer.Initialize(null, name, color, mediaModel, ghostVisuals);
        ghostVisuals.Initialize(name, userModel.SteamId, color);

        AddGhost(mediaModel, ghost);
    }

    protected void SpawnGhost(
        int identifier,
        int ghostId,
        string ghostUrl,
        string displayName,
        string steamId,
        Color? color
    )
    {
        if (identifier != validIdentifier)
            return;

        GameObject ghost = new($"Ghost for {steamId}");
        var ghostPlayer = ghost.AddComponent<GhostPlayer>();
        var ghostVisuals = ghost.AddComponent<GhostVisuals>();

        ghostPlayer.Initialize(null, displayName, color, ghostId, ghostUrl, ghostVisuals);
        ghostVisuals.Initialize(displayName, steamId, color);

        AddGhost(new MediaResponseModel { GhostUrl = ghostUrl }, ghost);
    }
}