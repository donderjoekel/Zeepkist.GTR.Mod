using System;
using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Assets;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TNRD.Zeepkist.GTR.UI.Medals;

/// <summary>Owns only the medal slot. Shared leaderboard rows must release it on tab changes.</summary>
public sealed class RecordMedalUi : MonoBehaviour
{
    private RectTransform _time;
    private Vector2 _originalOffsetMin;
    private Image _current;
    private Image _incoming;
    private bool _reserved;
    private int? _target;
    private MedalUpgrade _lastUpgrade;
    private Coroutine _animation;
    private ConfigService _config;
    private AssetService _assets;
    private bool _bound;
    private bool _owned;
    private string _identity;
    private RectTransform _holderName;
    private Vector2 _originalNameOffsetMax;
    private TextMeshProUGUI _leaderboardName;
    private Vector2 _originalTimeAnchorMin;
    private Vector2 _originalTimeAnchorMax;
    private Vector2 _originalTimeOffsetMin;
    private Vector2 _originalTimeOffsetMax;
    private Vector2 _originalLeaderboardNameAnchorMax;
    private Vector2 _originalLeaderboardNameOffsetMax;
    private bool _originalNameWrapping;
    private TextOverflowModes _originalNameOverflow;
    private float _leaderboardTimeWidth;
    private float _leaderboardMedalSize;
    private bool _updatingLayout;

    public static void ConfigureLeaderboard(TextMeshProUGUI time, TextMeshProUGUI playerName)
    {
        RecordMedalUi ui = time.GetComponent<RecordMedalUi>();
        if (ui == null) ui = time.gameObject.AddComponent<RecordMedalUi>();
        if (ui._leaderboardName != null) return;
        ui._time = time.rectTransform;
        ui._leaderboardName = playerName;
        ui._originalTimeAnchorMin = ui._time.anchorMin;
        ui._originalTimeAnchorMax = ui._time.anchorMax;
        ui._originalTimeOffsetMin = ui._time.offsetMin;
        ui._originalTimeOffsetMax = ui._time.offsetMax;
        ui._originalLeaderboardNameAnchorMax = playerName.rectTransform.anchorMax;
        ui._originalLeaderboardNameOffsetMax = playerName.rectTransform.offsetMax;
        ui._originalNameWrapping = playerName.enableWordWrapping;
        ui._originalNameOverflow = playerName.overflowMode;

    }

    public static void ConfigureHolder(TextMeshProUGUI time, TextMeshProUGUI playerName)
    {
        RecordMedalUi ui = time.GetComponent<RecordMedalUi>();
        if (ui == null) ui = time.gameObject.AddComponent<RecordMedalUi>();
        ui._time = time.rectTransform;
        ui._holderName = playerName.rectTransform;
        ui._originalNameOffsetMax = ui._holderName.offsetMax;
        playerName.enableWordWrapping = false;
        playerName.overflowMode = TextOverflowModes.Ellipsis;
    }

    public static void Set(TextMeshProUGUI time, int? medal, MedalUpgrade upgrade = null, string identity = null)
    {
        RecordMedalUi ui = time.GetComponent<RecordMedalUi>();
        if (ui == null) ui = time.gameObject.AddComponent<RecordMedalUi>();
        ui._owned = true;
        ui.Apply(time, medal, upgrade, identity);
    }

    public static void Clear(TextMeshProUGUI time)
    {
        RecordMedalUi ui = time.GetComponent<RecordMedalUi>();
        if (ui == null) return;
        ui._owned = false;
        ui._bound = false;
        ui._target = null;
        ui.Release();
    }

    private void Apply(TextMeshProUGUI time, int? medal, MedalUpgrade upgrade, string identity)
    {
        _assets ??= ServiceHelper.Instance.GetRequiredService<AssetService>();
        if (_config == null)
        {
            _config = ServiceHelper.Instance.GetRequiredService<ConfigService>();
            _config.AnimateMedalUpgrades.SettingChanged += OnAnimationSettingChanged;
        }

        medal = MedalUpgradeTracker.Normalize(medal);
        bool sameIdentity = string.Equals(_identity, identity, StringComparison.Ordinal);
        // A confirmed PB can move to a different visible row when its rank improves.
        // The subscription tracker establishes baselines for newly visible players.
        bool canAnimate = _bound && isActiveAndEnabled;
        _bound = true;
        _identity = identity;
        bool freshUpgrade = upgrade != null && !ReferenceEquals(upgrade, _lastUpgrade);
        if (upgrade != null) _lastUpgrade = upgrade;
        bool sameTarget = _target == medal;
        _target = medal;
        _time = time.rectTransform;
        if (!_reserved)
        {
            _originalOffsetMin = _time.offsetMin;
            float size = GetMedalSize(time);
            // Holders borrow space from the name instead of shrinking the time field.
            if (_leaderboardName != null)
                ApplyLeaderboardLayout(size);
            else if (_holderName == null)
                _time.offsetMin += new Vector2(size + 4, 0);
            _current ??= CreateImage("GTR Medal", size);
            _incoming ??= CreateImage("GTR Incoming Medal", size);
            _reserved = true;
        }

        if (sameIdentity && sameTarget && _animation != null && !freshUpgrade) return;
        Finish();
        if (freshUpgrade && canAnimate && _config.AnimateMedalUpgrades.Value)
            _animation = StartCoroutine(Slam(upgrade));
    }

    private float GetMedalSize(TextMeshProUGUI time)
    {
        if (_leaderboardName != null)
        {
            RectTransform name = _leaderboardName.rectTransform;
            Vector3 left = _time.parent.InverseTransformPoint(name.TransformPoint(new Vector3(name.rect.xMin, 0, 0)));
            Vector3 right = _time.parent.InverseTransformPoint(_time.TransformPoint(new Vector3(_time.rect.xMax, 0, 0)));
            float width = Mathf.Max(1, right.x - left.x);
            _leaderboardTimeWidth = width * 0.32f;
            _leaderboardMedalSize = Mathf.Max(1, Mathf.Min(_time.rect.height * 0.9f, width * 0.08f));
            return _leaderboardMedalSize;
        }
        if (_holderName == null) return time.fontSize * 1.25f;

        float nameWidth = _holderName.rect.width + _originalNameOffsetMax.x - _holderName.offsetMax.x;
        float borrowedWidth = nameWidth * 0.35f;
        _holderName.offsetMax = _originalNameOffsetMax - new Vector2(borrowedWidth, 0);
        return Mathf.Max(1, Mathf.Min(_time.rect.height * 0.95f, borrowedWidth - 4));
    }

    private void ApplyLeaderboardLayout(float size)
    {
        // Pin the whole time column to its original right edge. Medal stays outside that column.
        _time.anchorMax = _originalTimeAnchorMax;
        _time.anchorMin = new Vector2(_originalTimeAnchorMax.x, _originalTimeAnchorMin.y);
        _time.offsetMax = _originalTimeOffsetMax;
        _time.offsetMin = new Vector2(_originalTimeOffsetMax.x - _leaderboardTimeWidth, _originalTimeOffsetMin.y);
        RectTransform name = _leaderboardName.rectTransform;
        // These fields may have different parents. Convert edges instead of copying anchor offsets.
        float right = name.parent.InverseTransformPoint(
            _time.TransformPoint(new Vector3(_time.rect.xMin - size - 8, 0, 0))).x;
        float currentRight = name.parent.InverseTransformPoint(
            name.TransformPoint(new Vector3(name.rect.xMax, 0, 0))).x;
        name.offsetMax += new Vector2(right - currentRight, 0);
        _leaderboardName.enableWordWrapping = false;
        _leaderboardName.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!_reserved || _updatingLayout || (_holderName == null && _leaderboardName == null)) return;
        _updatingLayout = true;
        try
        {
            Finish();
            float size = GetMedalSize(GetComponent<TextMeshProUGUI>());
            if (_leaderboardName != null) ApplyLeaderboardLayout(size);
            foreach (Image image in new[] { _current, _incoming })
            {
                image.rectTransform.sizeDelta = new Vector2(size, size);
                image.rectTransform.anchoredPosition = new Vector2(-size / 2 - 4, 0);
            }
        }
        finally { _updatingLayout = false; }
    }

    private Image CreateImage(string name, float size)
    {
        var holder = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = holder.GetComponent<RectTransform>();
        rect.SetParent(_time, false);
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = new Vector2(-size / 2 - 4, 0);
        Image image = holder.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private IEnumerator Slam(MedalUpgrade upgrade)
    {
        Show(_current, upgrade.First > 0 ? upgrade.First - 1 : (int?)null);
        for (int tier = upgrade.First; tier <= upgrade.Last; tier++)
        {
            Show(_incoming, tier);
            float elapsed = 0;
            Vector2 rest = _current.rectTransform.anchoredPosition;
            while (elapsed < 0.28f)
            {
                // Fast accelerating descent; impact at 120 ms, compression, then settle.
                if (elapsed < 0.12f)
                {
                    float progress = elapsed / 0.12f;
                    float remaining = 1 - progress * progress;
                    _incoming.rectTransform.localScale = Vector3.one * (1 + 0.7f * remaining);
                    _incoming.rectTransform.anchoredPosition = rest + Vector2.up * (rest.x * -0.4f * remaining);
                }
                else
                {
                    float impact = elapsed - 0.12f;
                    float squash = impact < 0.04f ? impact / 0.04f : 1 - (impact - 0.04f) / 0.12f;
                    squash = Mathf.Clamp01(squash);
                    _incoming.rectTransform.localScale = new Vector3(1 + 0.12f * squash, 1 - 0.12f * squash, 1);
                    _incoming.rectTransform.anchoredPosition = rest;
                    _current.color = new Color(1, 1, 1, 1 - Mathf.Clamp01(impact / 0.04f));
                }
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            Show(_current, tier);
            _incoming.enabled = false;
        }
        _animation = null;
        RestoreTransforms();
    }

    private void Show(Image image, int? medal)
    {
        image.sprite = medal.HasValue ? _assets.GetMedalSprite(medal.Value) : null;
        image.enabled = image.sprite != null && _reserved;
        image.color = Color.white;
    }

    private void RestoreTransforms()
    {
        if (_current == null) return;
        _current.rectTransform.localScale = Vector3.one;
        _current.color = Color.white;
        _incoming.rectTransform.localScale = Vector3.one;
        _incoming.rectTransform.anchoredPosition = _current.rectTransform.anchoredPosition;
        _incoming.color = Color.white;
    }

    private void Finish()
    {
        if (_animation != null) StopCoroutine(_animation);
        _animation = null;
        if (_current == null) return;
        RestoreTransforms();
        Show(_current, _target);
        _incoming.enabled = false;
    }

    private void OnAnimationSettingChanged(object sender, EventArgs args)
    {
        if (!_config.AnimateMedalUpgrades.Value) Finish();
    }

    private void Release()
    {
        Finish();
        bool reserved = _reserved;
        _reserved = false;
        if (reserved && _leaderboardName != null)
        {
            _time.anchorMin = _originalTimeAnchorMin;
            _time.anchorMax = _originalTimeAnchorMax;
            _time.offsetMin = _originalTimeOffsetMin;
            _time.offsetMax = _originalTimeOffsetMax;
            _leaderboardName.rectTransform.anchorMax = _originalLeaderboardNameAnchorMax;
            _leaderboardName.rectTransform.offsetMax = _originalLeaderboardNameOffsetMax;
            _leaderboardName.enableWordWrapping = _originalNameWrapping;
            _leaderboardName.overflowMode = _originalNameOverflow;
        }
        else if (reserved) _time.offsetMin = _originalOffsetMin;
        if (_holderName != null) _holderName.offsetMax = _originalNameOffsetMax;
        if (_current != null) _current.enabled = false;
        if (_incoming != null) _incoming.enabled = false;
    }

    private void OnEnable()
    {
        if (_owned && _time != null) Apply(GetComponent<TextMeshProUGUI>(), _target, null, _identity);
    }

    private void OnDisable()
    {
        _bound = false;
        Release();
    }

    private void OnDestroy()
    {
        Release();
        if (_config != null) _config.AnimateMedalUpgrades.SettingChanged -= OnAnimationSettingChanged;
    }
}
