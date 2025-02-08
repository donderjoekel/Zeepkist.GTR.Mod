using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI;

public class RecordHolderUi : MonoBehaviour
{
    private record Wrapper(RectTransform RectTransform, RecordHolderUi Ui);

    private static RecordHolderUi _combinedInstance;
    private static RecordHolderUi _worldRecordInstance;
    private static RecordHolderUi _personalBestInstance;
    private static WorldRecordHolderUi _combinedWorldRecordHolderUi;
    private static WorldRecordHolderUi _singleWorldRecordHolderUi;
    private static PersonalBestHolderUi _combinedPersonalBestHolderUi;
    private static PersonalBestHolderUi _singlePersonalBestHolderUi;

    public static void EnsureExists()
    {
        GetCombinedInstance();
    }

    public static void Create(RecordHolders recordHolders)
    {
        ConfigService configService = ServiceHelper.Instance.GetRequiredService<ConfigService>();
        GetCombinedInstance().Initialize(configService.ShowRecordHolder, _combinedWorldRecordHolderUi,
            _combinedPersonalBestHolderUi);
        GetCombinedInstance().SetRecordHolders(recordHolders);
        GetCombinedInstance().ToggleDisplay();
        GetWorldRecordInstance().Initialize(configService.ShowWorldRecordHolder, _singleWorldRecordHolderUi, null);
        GetWorldRecordInstance().SetRecordHolders(recordHolders);
        GetCombinedInstance().ToggleDisplay();
        GetPersonalBestInstance()
            .Initialize(configService.ShowPersonalBestHolder, null, _singlePersonalBestHolderUi);
        GetPersonalBestInstance().SetRecordHolders(recordHolders);
        GetPersonalBestInstance().ToggleDisplay();
    }

    public static void SwitchToNext()
    {
        RecordHolderUi instance = GetCombinedInstance();
        if (instance == null)
        {
            // This should not happen???
            return;
        }

        instance.ToggleDisplay();
    }

    public static void Disable()
    {
        GetCombinedInstance().gameObject.SetActive(false);
    }

    private static Wrapper CreateWrapper(string name)
    {
        RectTransform template = GetTemplate();
        if (template == null)
            return null;

        GameObject recordHolders = new(name, typeof(RectTransform));
        RectTransform instance = recordHolders.GetComponent<RectTransform>();
        instance.SetParent(template.parent);
        instance.anchorMin = new Vector2(0.82f, 0.2f);
        instance.anchorMax = new Vector2(1f, 0.28f);
        instance.offsetMin = Vector2.zero;
        instance.offsetMax = Vector2.zero;
        instance.anchoredPosition = Vector2.zero;
        instance.anchoredPosition3D = Vector3.zero;
        instance.sizeDelta = Vector2.zero;
        UIApi.AddToConfigurator(instance);

        return new Wrapper(instance, instance.gameObject.AddComponent<RecordHolderUi>());
    }

    private static RectTransform GetTemplate()
    {
        OnlineGameplayUI onlineGameplayUi = FindObjectOfType<OnlineGameplayUI>(true);
        if (onlineGameplayUi == null)
            return null;

        RectTransform[] rectTransforms = onlineGameplayUi.GetComponentsInChildren<RectTransform>(true);
        return rectTransforms.FirstOrDefault(x => string.Equals(x.name, "WR", StringComparison.OrdinalIgnoreCase));
    }

    private static RecordHolderUi GetCombinedInstance()
    {
        if (_combinedInstance != null)
            return _combinedInstance;

        Wrapper wrapper = CreateWrapper("CombinedHolder");
        if (wrapper == null)
            return null;

        _combinedInstance = wrapper.Ui;
        _combinedWorldRecordHolderUi = CreateWorldRecordHolderUi(wrapper.RectTransform);
        _combinedPersonalBestHolderUi = CreatePersonalBestHolderUi(wrapper.RectTransform);
        return _combinedInstance;
    }

    private static RecordHolderUi GetWorldRecordInstance()
    {
        if (_worldRecordInstance != null)
            return _worldRecordInstance;

        Wrapper wrapper = CreateWrapper("WorldRecordHolder");
        if (wrapper == null)
            return null;

        _worldRecordInstance = wrapper.Ui;
        _singleWorldRecordHolderUi = CreateWorldRecordHolderUi(wrapper.RectTransform);
        return _worldRecordInstance;
    }

    private static RecordHolderUi GetPersonalBestInstance()
    {
        if (_personalBestInstance != null)
            return _personalBestInstance;

        Wrapper wrapper = CreateWrapper("PersonalBestHolder");
        if (wrapper == null)
            return null;

        _personalBestInstance = wrapper.Ui;
        _singlePersonalBestHolderUi = CreatePersonalBestHolderUi(wrapper.RectTransform);
        return _personalBestInstance;
    }

    private static WorldRecordHolderUi CreateWorldRecordHolderUi(RectTransform instance)
    {
        RectTransform template = GetTemplate();
        RectTransform worldRecordInstance = CreateInstance(template, instance);
        worldRecordInstance.gameObject.SetActive(false);
        worldRecordInstance.anchorMin = Vector2.zero;
        worldRecordInstance.anchorMax = Vector2.one;
        WorldRecordHolderUi ui = worldRecordInstance.gameObject.AddComponent<WorldRecordHolderUi>();
        ui.InitializeUi();
        return ui;
    }

    private static PersonalBestHolderUi CreatePersonalBestHolderUi(RectTransform instance)
    {
        RectTransform template = GetTemplate();
        RectTransform personalBestInstance = CreateInstance(template, instance);
        personalBestInstance.gameObject.SetActive(false);
        personalBestInstance.anchorMin = Vector2.zero;
        personalBestInstance.anchorMax = Vector2.one;
        PersonalBestHolderUi ui = personalBestInstance.gameObject.AddComponent<PersonalBestHolderUi>();
        ui.InitializeUi();
        return ui;
    }

    private static RectTransform CreateInstance(RectTransform template, Transform parent)
    {
        RectTransform instance = Instantiate(template, parent);
        GUI_OnlineLeaderboardPosition component = instance.GetComponentInChildren<GUI_OnlineLeaderboardPosition>();
        Destroy(component);
        return instance;
    }

    private readonly List<ToggleAction> _toggleActions = [];

    private ConfigEntry<bool> _showConfig;
    private WorldRecordHolderUi _worldRecordHolderUi;
    private PersonalBestHolderUi _personalBestHolderUi;

    private RecordHolders _recordHolders;
    private ConfigService _configService;

    private ConfigService ConfigService =>
        _configService ??= ServiceHelper.Instance.GetRequiredService<ConfigService>();

    private void Awake()
    {
        ConfigService.ShowRecordHolder.SettingChanged += OnShowRecordHolderChanged;
        ConfigService.ShowWorldRecordOnHolder.SettingChanged += OnShowWorldRecordChanged;
        ConfigService.ShowPersonalBestOnHolder.SettingChanged += OnShowPersonalBestChanged;
    }

    private void OnDestroy()
    {
        ConfigService.ShowRecordHolder.SettingChanged -= OnShowRecordHolderChanged;
        ConfigService.ShowWorldRecordOnHolder.SettingChanged -= OnShowWorldRecordChanged;
        ConfigService.ShowPersonalBestOnHolder.SettingChanged -= OnShowPersonalBestChanged;
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

    private void Initialize(ConfigEntry<bool> showConfig, WorldRecordHolderUi worldRecordHolderUi,
        PersonalBestHolderUi personalBestHolderUi)
    {
        _showConfig = showConfig;
        _worldRecordHolderUi = worldRecordHolderUi;
        _personalBestHolderUi = personalBestHolderUi;
    }
    
    private void SetRecordHolders(RecordHolders recordHolders)
    {
        _recordHolders = recordHolders;
        if (_worldRecordHolderUi != null)
            _worldRecordHolderUi.SetWorldRecordHolder(_recordHolders.WorldRecord);
        if (_personalBestHolderUi != null)
            _personalBestHolderUi.SetPersonalBestHolder(_recordHolders.PersonalBest);
        UpdateDisplayActions();
    }

    private void UpdateDisplayActions()
    {
        gameObject.SetActive(_showConfig.Value);

        if (_personalBestHolderUi == null || _worldRecordHolderUi == null)
        {
            if (_personalBestHolderUi != null)
            {
                _personalBestHolderUi.gameObject.SetActive(true);
            }

            if (_worldRecordHolderUi != null)
            {
                _worldRecordHolderUi.gameObject.SetActive(true);
            }
        }
        else
        {
            _worldRecordHolderUi.gameObject.SetActive(false);
            _personalBestHolderUi.gameObject.SetActive(false);
            _toggleActions.Clear();

            if (ConfigService.ShowWorldRecordOnHolder.Value)
            {
                _toggleActions.Add(new ToggleAction(_worldRecordHolderUi.gameObject,
                    _personalBestHolderUi.gameObject));
            }

            if (ConfigService.ShowPersonalBestOnHolder.Value)
            {
                _toggleActions.Add(new ToggleAction(_personalBestHolderUi.gameObject,
                    _worldRecordHolderUi.gameObject));
            }
        }
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
