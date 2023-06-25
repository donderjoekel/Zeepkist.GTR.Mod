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
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Mod.Components;

internal class WorldRecordHolderUi : MonoBehaviour
{
    private const float SWITCH_DURATION = 15;

    private ManualLogSource logger;
    private RectTransform uiHolder;
    private Image worldRecordImage;
    private TextMeshProUGUI headerText;
    private TextMeshProUGUI playerNameText;
    private TextMeshProUGUI timeText;
    private bool hasLoadedLevel;

    private RecordResponseModel worldRecord;
    private RecordResponseModel personalBest;
    private float switchCountdown;
    private bool isShowingWorldRecord;

    private void Awake()
    {
        logger = EntryPoint.CreateLogger(nameof(WorldRecordHolderUi));

        RacingApi.PlayerSpawned += OnSpawnPlayers;
        InternalLevelApi.LevelCreating += InternalLevelApiOnLevelCreating;
        InternalLevelApi.LevelCreated += InternalLevelApiOnLevelCreated;

        Plugin.ConfigShowWorldRecordHolder.SettingChanged += OnShowSettingChanged;
    }

    private void OnDestroy()
    {
        RacingApi.PlayerSpawned -= OnSpawnPlayers;
        InternalLevelApi.LevelCreating -= InternalLevelApiOnLevelCreating;
        InternalLevelApi.LevelCreated -= InternalLevelApiOnLevelCreated;

        Plugin.ConfigShowWorldRecordHolder.SettingChanged -= OnShowSettingChanged;
    }

    private void OnShowSettingChanged(object sender, EventArgs e)
    {
        if (uiHolder != null)
            uiHolder.gameObject.SetActive(Plugin.ConfigShowWorldRecordHolder.Value);
    }

    private void InternalLevelApiOnLevelCreating()
    {
        worldRecord = null;
        personalBest = null;
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

        Result<RecordsGetResponseDTO> getWorldRecordResult = await SdkWrapper.Instance.RecordsApi.Get(builder =>
            builder.WithLevelId(InternalLevelApi.CurrentLevelId).WithWorldRecordOnly(true));

        Result<RecordsGetResponseDTO> getPersonalBestResult = await SdkWrapper.Instance.RecordsApi.Get(builder =>
            builder.WithLevelId(InternalLevelApi.CurrentLevelId)
                .WithBestOnly(true)
                .WithUserId(SdkWrapper.Instance.UsersApi.UserId));

        if (getWorldRecordResult.IsSuccess)
        {
            worldRecord = getWorldRecordResult.Value.Records.FirstOrDefault();
        }
        else
        {
            logger.LogError($"Unable to get world record: {getWorldRecordResult}");
        }

        if (getPersonalBestResult.IsSuccess)
        {
            personalBest = getPersonalBestResult.Value.Records.FirstOrDefault();
        }
        else
        {
            logger.LogError($"Unable to get personal best: {getPersonalBestResult}");
        }

        if (worldRecord == null && personalBest == null)
        {
            uiHolder.gameObject.SetActive(false);
            return;
        }

        switchCountdown = SWITCH_DURATION;
        isShowingWorldRecord = worldRecord != null;
        UpdateUi(worldRecord ?? personalBest);
    }

    private void UpdateUi(RecordResponseModel record)
    {
        headerText.text = isShowingWorldRecord ? "World Record" : "Personal Best";
        worldRecordImage.enabled = isShowingWorldRecord;
        playerNameText.text = record.User!.SteamName;
        timeText.text = record.Time!.Value.GetFormattedTime();
        uiHolder.gameObject.SetActive(Plugin.ConfigShowWorldRecordHolder.Value);
    }

    private void OnSpawnPlayers()
    {
        if (!ZeepkistNetwork.IsConnectedToGame)
        {
            logger.LogInfo("Not online so can ignore");
            return;
        }

        CreateUI();
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

        headerText =
            texts.FirstOrDefault(x => string.Equals(x.name, "Your Time Title", StringComparison.OrdinalIgnoreCase));
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
        worldRecordImage = positionGameObject.AddComponent<Image>();
        worldRecordImage.preserveAspect = true;
        worldRecordImage.sprite = PlayerManager.Instance.youTriedMedal;
    }

    private void Update()
    {
        if (!ZeepkistNetwork.IsConnected || ZeepkistNetwork.CurrentLobby == null)
            return;
        
        if (worldRecord != null && personalBest != null)
        {
            if (switchCountdown <= 0)
            {
                switchCountdown = SWITCH_DURATION;
                isShowingWorldRecord = !isShowingWorldRecord;
                UpdateUi(isShowingWorldRecord ? worldRecord : personalBest);
            }

            switchCountdown -= Time.deltaTime;
        }

        if (Input.GetKeyDown(Plugin.ConfigToggleShowWorldRecordHolder.Value))
        {
            Plugin.ConfigShowWorldRecordHolder.Value = !Plugin.ConfigShowWorldRecordHolder.Value;

            PlayerManager.Instance.messenger.Log(Plugin.ConfigShowWorldRecordHolder.Value
                    ? "Showing World Record Holder"
                    : "Hiding World Record Holder",
                2.5f);
        }
    }
}
