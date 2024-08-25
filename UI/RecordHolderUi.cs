using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.UI;

public class RecordHolderUi : MonoBehaviour
{
    private static RecordHolderUi _instance;
    private static WorldRecordHolderUi _worldRecordHolderUi;
    private static PersonalBestHolderUi _personalBestHolderUi;

    public static void EnsureExists()
    {
        GetInstance();
    }

    public static void Create(RecordHolders recordHolders)
    {
        GetInstance().SetRecordHolders(recordHolders);
        GetInstance().ToggleDisplay();
    }

    public static void SwitchToNext()
    {
        GetInstance().ToggleDisplay();
    }

    public static void Disable()
    {
        GetInstance().gameObject.SetActive(false);
    }

    private static RecordHolderUi GetInstance()
    {
        if (_instance != null)
            return _instance;

        OnlineGameplayUI onlineGameplayUi = FindObjectOfType<OnlineGameplayUI>(true);
        if (onlineGameplayUi == null)
            return null;

        RectTransform[] rectTransforms = onlineGameplayUi.GetComponentsInChildren<RectTransform>(true);

        RectTransform template
            = rectTransforms.FirstOrDefault(x => string.Equals(x.name, "WR", StringComparison.OrdinalIgnoreCase));

        if (template == null)
            return null;

        GameObject recordHolders = new("RecordHolders", typeof(RectTransform));
        RectTransform instance = recordHolders.GetComponent<RectTransform>();
        instance.SetParent(template.parent);
        instance.anchorMin = new Vector2(0.82f, 0.2f);
        instance.anchorMax = new Vector2(1f, 0.28f);
        instance.offsetMin = Vector2.zero;
        instance.offsetMax = Vector2.zero;
        instance.anchoredPosition = Vector2.zero;
        instance.anchoredPosition3D = Vector3.zero;
        instance.sizeDelta = Vector2.zero;

        _instance = instance.gameObject.AddComponent<RecordHolderUi>();

        RectTransform worldRecordInstance = CreateInstance(template, instance);
        worldRecordInstance.gameObject.SetActive(false);
        worldRecordInstance.anchorMin = Vector2.zero;
        worldRecordInstance.anchorMax = Vector2.one;
        _worldRecordHolderUi = worldRecordInstance.gameObject.AddComponent<WorldRecordHolderUi>();
        _worldRecordHolderUi.InitializeUi();

        RectTransform personalBestInstance = CreateInstance(template, instance);
        personalBestInstance.gameObject.SetActive(false);
        personalBestInstance.anchorMin = Vector2.zero;
        personalBestInstance.anchorMax = Vector2.one;
        _personalBestHolderUi = personalBestInstance.gameObject.AddComponent<PersonalBestHolderUi>();
        _personalBestHolderUi.InitializeUi();

        return _instance;
    }

    private static RectTransform CreateInstance(RectTransform template, Transform parent)
    {
        RectTransform instance = Instantiate(template, parent);
        GUI_OnlineLeaderboardPosition component = instance.GetComponentInChildren<GUI_OnlineLeaderboardPosition>();
        Destroy(component);
        return instance;
    }

    private readonly List<ToggleAction> _toggleActions = new();

    private RecordHolders _recordHolders;
    private ConfigService _configService;

    private void Awake()
    {
        _configService = ServiceHelper.Instance.GetRequiredService<ConfigService>();
        _configService.ShowRecordHolder.SettingChanged += OnShowRecordHolderChanged;
        _configService.ShowWorldRecordOnHolder.SettingChanged += OnShowWorldRecordChanged;
        _configService.ShowPersonalBestOnHolder.SettingChanged += OnShowPersonalBestChanged;
    }

    private void OnDestroy()
    {
        _configService.ShowRecordHolder.SettingChanged -= OnShowRecordHolderChanged;
        _configService.ShowWorldRecordOnHolder.SettingChanged -= OnShowWorldRecordChanged;
        _configService.ShowPersonalBestOnHolder.SettingChanged -= OnShowPersonalBestChanged;
    }

    private void OnShowRecordHolderChanged(object sender, EventArgs e)
    {
        UpdateDisplayActions();
        ToggleDisplay();
    }

    private void OnShowWorldRecordChanged(object sender, EventArgs e)
    {
        UpdateDisplayActions();
        ToggleDisplay();
    }

    private void OnShowPersonalBestChanged(object sender, EventArgs e)
    {
        UpdateDisplayActions();
        ToggleDisplay();
    }

    private void SetRecordHolders(RecordHolders recordHolders)
    {
        _recordHolders = recordHolders;
        _worldRecordHolderUi.SetWorldRecordHolder(_recordHolders.WorldRecord);
        _personalBestHolderUi.SetPersonalBestHolder(_recordHolders.PersonalBest);
        UpdateDisplayActions();
    }

    private void UpdateDisplayActions()
    {
        gameObject.SetActive(_configService.ShowRecordHolder.Value);
        _worldRecordHolderUi.gameObject.SetActive(false);
        _personalBestHolderUi.gameObject.SetActive(false);
        _toggleActions.Clear();
        if (_configService.ShowWorldRecordOnHolder.Value)
            _toggleActions.Add(new ToggleAction(_worldRecordHolderUi.gameObject, _personalBestHolderUi.gameObject));
        if (_configService.ShowPersonalBestOnHolder.Value)
            _toggleActions.Add(new ToggleAction(_personalBestHolderUi.gameObject, _worldRecordHolderUi.gameObject));
    }

    private void ToggleDisplay()
    {
        if (_toggleActions.Count == 0)
            return;

        ToggleAction toggleAction = _toggleActions.First();
        _toggleActions.RemoveAt(0);
        toggleAction.Toggle();
        _toggleActions.Add(toggleAction);
    }

    private class ToggleAction
    {
        private readonly GameObject _toEnable;
        private readonly GameObject _toDisable;

        public ToggleAction(GameObject toEnable, GameObject toDisable)
        {
            _toEnable = toEnable;
            _toDisable = toDisable;
        }

        public void Toggle()
        {
            _toEnable.SetActive(true);
            _toDisable.SetActive(false);
        }
    }
}
