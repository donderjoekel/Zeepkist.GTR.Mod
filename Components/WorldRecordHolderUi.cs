using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using TMPro;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.Mod.Patches;
using TNRD.Zeepkist.GTR.SDK;
using UnityEngine;
using UnityEngine.UI;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Mod.Components;

internal class WorldRecordHolderUi : MonoBehaviour
{
    private ManualLogSource logger;
    private RectTransform uiHolder;
    private TextMeshProUGUI playerNameText;
    private TextMeshProUGUI timeText;
    private bool hasLoadedLevel;

    private void Awake()
    {
        logger = Plugin.CreateLogger(nameof(WorldRecordHolderUi));

        GameMaster_SpawnPlayers.SpawnPlayers += OnSpawnPlayers;
        InternalLevelApi.LevelCreating += InternalLevelApiOnLevelCreating;
        InternalLevelApi.LevelCreated += InternalLevelApiOnLevelCreated;
    }

    private void OnDestroy()
    {
        GameMaster_SpawnPlayers.SpawnPlayers -= OnSpawnPlayers;
        InternalLevelApi.LevelCreating -= InternalLevelApiOnLevelCreating;
        InternalLevelApi.LevelCreated -= InternalLevelApiOnLevelCreated;
    }

    private void InternalLevelApiOnLevelCreating()
    {
        hasLoadedLevel = false;
        if (uiHolder != null)
            uiHolder.gameObject.SetActive(false);
    }

    private void InternalLevelApiOnLevelCreated()
    {
        hasLoadedLevel = true;
        LoadWorldRecord().Forget();
    }

    private async UniTaskVoid LoadWorldRecord()
    {
        if (!hasLoadedLevel)
            return;

        Result<RecordsGetResponseDTO> result = await RecordsApi.Get(builder =>
            builder.WithLevelId(InternalLevelApi.CurrentLevelId).WithWorldRecordOnly(true));

        if (result.IsFailed)
        {
            logger.LogError($"Unable to load world record: {result}");
            uiHolder.gameObject.SetActive(true);
            return;
        }

        if (result.Value.Records.Count == 0)
        {
            logger.LogInfo("No world record found");
            uiHolder.gameObject.SetActive(false);
            return;
        }

        RecordResponseModel record = result.Value.Records.First();
        playerNameText.text = record.User.SteamName;
        timeText.text = record.Time.Value.GetFormattedTime();
        uiHolder.gameObject.SetActive(true);
    }

    private void OnSpawnPlayers()
    {
        if (!ZeepkistNetwork.IsConnectedToGame)
        {
            logger.LogInfo("Not online so can ignore");
            return;
        }

        CreateUI();
        logger.LogInfo("Loading world record");
        LoadWorldRecord().Forget();
    }

    private void CreateUI()
    {
        if (uiHolder != null)
            return;

        OnlineGameplayUI onlineGameplayUi = FindObjectOfType<OnlineGameplayUI>(true);
        if (onlineGameplayUi == null)
        {
            logger.LogInfo("No online gameplay UI found");
            return;
        }

        RectTransform[] rectTransforms = onlineGameplayUi.GetComponentsInChildren<RectTransform>(true);

        RectTransform template = rectTransforms.FirstOrDefault(x =>
            string.Equals(x.name, "WR (for Saty)", StringComparison.OrdinalIgnoreCase));

        if (template == null)
        {
            logger.LogInfo("No template found");
            return;
        }

        uiHolder = Instantiate(template, template.parent);
        GUI_OnlineLeaderboardPosition guiOnlineLeaderboardPosition =
            uiHolder.GetComponentInChildren<GUI_OnlineLeaderboardPosition>();
        Destroy(guiOnlineLeaderboardPosition);

        Vector2 copyAnchorMax = uiHolder.anchorMax;
        copyAnchorMax.y = 0.28f;
        uiHolder.anchorMax = copyAnchorMax;

        Vector2 copyAnchorMin = uiHolder.anchorMin;
        copyAnchorMin.y = 0.2f;
        uiHolder.anchorMin = copyAnchorMin;

        TextMeshProUGUI[] texts = uiHolder.GetComponentsInChildren<TextMeshProUGUI>();

        TextMeshProUGUI position =
            texts.FirstOrDefault(x => string.Equals(x.name, "Position", StringComparison.OrdinalIgnoreCase));

        StartCoroutine(ReplacePositionWithStar(position));

        playerNameText = texts.FirstOrDefault(x =>
            string.Equals(x.name, "Player", StringComparison.OrdinalIgnoreCase));
        timeText = texts.FirstOrDefault(x =>
            string.Equals(x.name, "Time", StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerator ReplacePositionWithStar(TextMeshProUGUI position)
    {
        GameObject positionGameObject = position.gameObject;
        Destroy(position);
        yield return null;
        Image image = positionGameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.sprite = PlayerManager.Instance.youTriedMedal;
    }
}
