using System.Linq;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Components.Ghosting;
using TNRD.Zeepkist.GTR.Mod.Patches;
using UnityEngine;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Mod.Components;

public abstract class BaseGhostLoader : MonoBehaviourWithLogging
{
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
        LoadGhosts().Forget();
    }

    protected abstract bool ContainsGhost(RecordResponseModel recordModel);
    protected abstract void AddGhost(RecordResponseModel recordModel, GameObject ghost);
    protected abstract void ClearGhosts();
    protected abstract UniTaskVoid LoadGhosts();

    protected static RecordResponseModel GetWorldRecordRecordModel(IResult<RecordsGetResponseDTO> wrGhost)
    {
        RecordResponseModel wr = null;
        if (wrGhost.IsSuccess)
        {
            if (wrGhost.Value.TotalAmount == 1)
                wr = wrGhost.Value.Records.First();
        }
        else
        {
            Logger.LogInfo("Loading world record failed");
            Logger.LogInfo(wrGhost.ToString());
        }

        return wr;
    }

    protected static RecordResponseModel GetPersonalBestRecordModel(IResult<RecordsGetResponseDTO> pbGhost)
    {
        RecordResponseModel pb = null;
        if (pbGhost.IsSuccess)
        {
            if (pbGhost.Value.TotalAmount == 1)
                pb = pbGhost.Value.Records.First();
        }
        else
        {
            Logger.LogInfo("Loading personal best failed");
            Logger.LogInfo(pbGhost.ToString());
        }

        return pb;
    }

    protected void SpawnWorldRecord(RecordResponseModel record)
    {
        if (record == null)
            return;
        string holderName = string.IsNullOrEmpty(record.User.SteamName)
            ? record.User.SteamId
            : record.User.SteamName;
        SpawnGhost(record, $"WR\n{holderName}\r\n{record.Time.Value!.GetFormattedTime()}", new Color(0.6f, 0, 0.8f));
    }

    protected void SpawnPersonalBest(RecordResponseModel record)
    {
        if (record == null)
            return;

        if (string.IsNullOrEmpty(record.GhostUrl))
        {
            Logger.LogInfo("Skipping ghost because there's no ghost url yet");
            return;
        }

        SpawnGhost(record, $"PB\n{record.Time.Value.GetFormattedTime()}", new Color(0, 0.7f, 0));
    }

    protected void SpawnGhost(RecordResponseModel recordModel, string name, Color? color)
    {
        if (ContainsGhost(recordModel))
            return;

        PhotonZeepkist photonZeepkist = FindObjectOfType<PhotonZeepkist>();
        NetworkedGhostSpawner networkedGhostSpawner = FindObjectOfType<NetworkedGhostSpawner>();

        NetworkedZeepkistGhost networkedZeepkistGhost = Instantiate(networkedGhostSpawner.zeepkistGhostPrefab,
            new Vector3(0, 5, 0),
            Quaternion.identity,
            photonZeepkist.physicsSceneTransform);

        GhostPlayer ghostPlayer = networkedZeepkistGhost.gameObject.AddComponent<GhostPlayer>();
        ghostPlayer.Initialize(networkedZeepkistGhost, name, color, recordModel);

        Ghost_AnimateWheel[] ghostAnimateWheels =
            ghostPlayer.gameObject.GetComponentsInChildren<Ghost_AnimateWheel>();

        for (int i = ghostAnimateWheels.Length - 1; i >= 0; i--)
        {
            Ghost_AnimateWheel ghostAnimateWheel = ghostAnimateWheels[i];
            Destroy(ghostAnimateWheel);
        }

        Destroy(networkedZeepkistGhost);

        AddGhost(recordModel, ghostPlayer.gameObject);
        // PlayerManager.Instance.currentMaster.flyingCamera.FlyingCamera.SetCurrentZeepkist();
    }
}
