using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using TMPro;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.SDK.Extensions;
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

    private float switchCountdown;

    private class Data
    {
        public Data(RecordResponseModel record, UserResponseModel user, bool isWorldRecord)
        {
            Record = record;
            User = user;
            IsWorldRecord = isWorldRecord;
        }

        public RecordResponseModel Record { get; private set; }
        public UserResponseModel User { get; private set; }
        public bool IsWorldRecord { get; private set; }
    }

    private List<Data> dataToShow = new();
    private int currentIndex = 0;

    private RecordResponseModel worldRecordRecord;
    private UserResponseModel worldRecordUser;

    private RecordResponseModel personalBestRecord;
    private UserResponseModel personalBestUser;

    private bool HasWorldRecord => worldRecordRecord != null && worldRecordUser != null;
    private bool HasPersonalBest => personalBestRecord != null && personalBestUser != null;

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
        dataToShow.Clear();
        worldRecordRecord = null;
        worldRecordUser = null;
        personalBestRecord = null;
        personalBestUser = null;

        hasLoadedLevel = false;
        if (uiHolder != null)
            uiHolder.gameObject.SetActive(false);
    }

    private void InternalLevelApiOnLevelCreated()
    {
        hasLoadedLevel = true;
        LoadRecords().Forget();
    }

    private void OnSpawnPlayers()
    {
        if (!ZeepkistNetwork.IsConnectedToGame)
        {
            logger.LogInfo("Not online so can ignore");
            return;
        }

        CreateUI();
        LoadRecords().Forget();
    }

    private async UniTaskVoid LoadRecords()
    {
        if (!hasLoadedLevel)
            return;

        dataToShow.Clear();
        currentIndex = 0;

        UniTask[] tasks =
        {
            GetWorldRecord(), GetPersonalBest()
        };

        await UniTask.WhenAll(tasks);

        if (!HasWorldRecord && !HasPersonalBest)
            return;

        if (HasWorldRecord)
        {
            dataToShow.Add(new Data(worldRecordRecord, worldRecordUser, true));
        }

        if (HasPersonalBest)
        {
            dataToShow.Add(new Data(personalBestRecord, personalBestUser, false));
        }

        switchCountdown = SWITCH_DURATION;
        UpdateUi();
    }

    private async UniTask GetWorldRecord()
    {
        Result<WorldRecordGetUiResponseDTO> result = await SdkWrapper.Instance.WorldRecordApi.GetUi(builder =>
        {
            builder
                .WithLevel(InternalLevelApi.CurrentLevelHash);
        });

        if (result.IsFailed)
        {
            if (!result.IsNotFound())
            {
                logger.LogError("Unable to get world record: " + result);
            }

            return;
        }

        worldRecordRecord = result.Value.Record;
        worldRecordUser = result.Value.User;
    }

    private async UniTask GetPersonalBest()
    {
        Result<PersonalBestGetUiResponseDTO> result = await SdkWrapper.Instance.PersonalBestApi.GetUi(builder =>
        {
            builder
                .WithLevel(InternalLevelApi.CurrentLevelHash)
                .WithUser(SdkWrapper.Instance.UsersApi.UserId);
        });

        if (result.IsFailed)
        {
            if (!result.IsNotFound())
            {
                logger.LogError("Unable to get personal best: " + result);
            }

            return;
        }

        personalBestRecord = result.Value.Record;
        personalBestUser = result.Value.User;
    }

    private void UpdateUi()
    {
        try
        {
            Data data = dataToShow[currentIndex];
            headerText.text = data.IsWorldRecord ? "World Record" : "Personal Best";
            worldRecordImage.enabled = data.IsWorldRecord;
            playerNameText.text = data.User.SteamName;
            timeText.text = data.Record.Time.GetFormattedTime();
            uiHolder.gameObject.SetActive(Plugin.ConfigShowWorldRecordHolder.Value);
        }
        catch (Exception)
        {
            // Ignore
        }
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

        if (uiHolder == null)
            return;

        if (dataToShow.Count <= 1)
            return;

        if (switchCountdown <= 0)
        {
            switchCountdown = SWITCH_DURATION;
            currentIndex = (currentIndex + 1) % dataToShow.Count;
            UpdateUi();
        }

        switchCountdown -= Time.deltaTime;

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
