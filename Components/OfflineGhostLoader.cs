using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.SDK;
using UnityEngine;
using ZeepkistClient;
using Random = UnityEngine.Random;

namespace TNRD.Zeepkist.GTR.Mod.Components;

public class OfflineGhostLoader : BaseGhostLoader
{
    private static readonly Dictionary<RecordResponseModel, GameObject> recordToGhost = new();

    public static IReadOnlyDictionary<RecordResponseModel, GameObject> RecordToGhost => recordToGhost;

    /// <inheritdoc />
    protected override bool ContainsGhost(RecordResponseModel recordModel)
    {
        return recordToGhost.ContainsKey(recordModel);
    }

    /// <inheritdoc />
    protected override void AddGhost(RecordResponseModel recordModel, GameObject ghost)
    {
        recordToGhost.Add(recordModel, ghost);
    }

    /// <inheritdoc />
    protected override void ClearGhosts()
    {
        foreach (KeyValuePair<RecordResponseModel, GameObject> kvp in recordToGhost)
        {
            Destroy(kvp.Value);
        }

        recordToGhost.Clear();
    }

    protected override async UniTaskVoid LoadGhosts()
    {
        if (ZeepkistNetwork.IsConnected)
            return;

        LevelScriptableObject level = PlayerManager.Instance.currentMaster.GlobalLevel;
        string globalLevelUid = level.UID;
        Result<RecordsGetResponseDTO> wrGhost =
            await Sdk.Instance.RecordsApi.Get(builder => builder.WithLevelUid(globalLevelUid).WithWorldRecordOnly(true));
        Result<RecordsGetResponseDTO> pbGhost =
            await Sdk.Instance.RecordsApi.Get(builder =>
                builder.WithLevelUid(globalLevelUid).WithBestOnly(true).WithUserId(Sdk.Instance.UsersApi.UserId));

        RecordResponseModel wr = GetWorldRecordRecordModel(wrGhost);
        RecordResponseModel pb = GetPersonalBestRecordModel(pbGhost);

        if (wr != null && pb != null)
        {
            if (wr.Id == pb.Id)
            {
                SpawnWorldRecord(wr);
            }
            else
            {
                SpawnWorldRecord(wr);
                SpawnPersonalBest(pb);
            }
        }
        else
        {
            SpawnWorldRecord(wr);
            SpawnPersonalBest(pb);
        }

        const int amountOfGhosts = 1;

        await UniTask.WhenAll(
            LoadMedalGhosts(level.TimeBronze, amountOfGhosts),
            LoadMedalGhosts(level.TimeSilver, amountOfGhosts),
            LoadMedalGhosts(level.TimeGold, amountOfGhosts),
            LoadMedalGhosts(level.TimeAuthor, amountOfGhosts),
            LoadRandomGhosts(10));
    }

    private async UniTask LoadMedalGhosts(float time, int amount)
    {
        const float timeTolerance = 1.5f;

        Result<RecordsGetResponseDTO> result = await Sdk.Instance.RecordsApi.Get(builder => builder
            .WithLevelUid(PlayerManager.Instance.currentMaster.GlobalLevel.UID)
            .WithMinimumTime(time - timeTolerance)
            .WithMaximumTime(time + timeTolerance)
            .WithWorldRecordOnly(false)
            .WithValidOnly(true));

        if (result.IsFailed)
        {
            Logger.LogError($"Unable to load medal ghosts ({time}): {result.ToString()}");
            return;
        }

        List<RecordResponseModel> records = result.Value.Records;
        if (records.Count == 0)
            return;

        if (amount > records.Count)
        {
            foreach (RecordResponseModel record in records)
            {
                SpawnGhostWithDefaultName(record);
            }
        }
        else
        {
            for (int i = 0; i < amount; i++)
            {
                int index = Random.Range(0, records.Count);
                RecordResponseModel record = records[index];
                records.RemoveAt(index);
                SpawnGhostWithDefaultName(record);
            }
        }
    }

    private async UniTask LoadRandomGhosts(int amount)
    {
        Result<RecordsGetResponseDTO> result = await Sdk.Instance.RecordsApi.Get(builder => builder
            .WithLevelUid(PlayerManager.Instance.currentMaster.GlobalLevel.UID)
            .WithWorldRecordOnly(false)
            .WithValidOnly(true));

        if (result.IsFailed)
        {
            Logger.LogError($"Unable to load random ghosts: {result.ToString()}");
            return;
        }

        if (result.Value.TotalAmount < amount)
        {
            List<UniTask> tasks = new List<UniTask>();

            for (int i = 0; i < result.Value.TotalAmount; i++)
            {
                tasks.Add(LoadRandomGhost(i));
            }

            await UniTask.WhenAll(tasks);
        }
        else
        {
            List<UniTask> tasks = new List<UniTask>();

            for (int i = 0; i < amount; i++)
            {
                int index = Random.Range(0, result.Value.TotalAmount);
                tasks.Add(LoadRandomGhost(index));
            }

            await UniTask.WhenAll(tasks);
        }
    }

    private async UniTask LoadRandomGhost(int index)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Zeepkist",
            "GTR",
            "Models");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string key = PlayerManager.Instance.currentMaster.GlobalLevel.UID + "_" + index + ".json";
        string filename = Path.Combine(folder, key);

        RecordResponseModel model;

        if (File.Exists(filename))
        {
            this.Logger().LogInfo($"Reading model from disk: {filename}");
            string contents = await File.ReadAllTextAsync(filename);
            model = JsonConvert.DeserializeObject<RecordResponseModel>(contents);
        }
        else
        {
            this.Logger().LogInfo($"Downloading model: {key}");
            Result<RecordsGetResponseDTO> result = await Sdk.Instance.RecordsApi.Get(builder => builder
                .WithLevelUid(PlayerManager.Instance.currentMaster.GlobalLevel.UID)
                .WithWorldRecordOnly(false)
                .WithValidOnly(true)
                .WithOffset(index)
                .WithLimit(1));

            if (result.IsFailed)
            {
                Logger.LogError($"Unable to load random ghost ({index}): {result.ToString()}");
                return;
            }

            if (result.Value.TotalAmount == 0)
                return;

            model = result.Value.Records[0];
        }

        SpawnGhostWithDefaultName(model);
        await File.WriteAllTextAsync(filename, JsonConvert.SerializeObject(model));
    }

    private void SpawnGhostWithDefaultName(RecordResponseModel record)
    {
        string holderName = string.IsNullOrEmpty(record.User.SteamName)
            ? record.User.SteamId
            : record.User.SteamName;
        SpawnGhost(record, $"{holderName}\r\n{record.Time.Value.GetFormattedTime()}", null);
    }
}
