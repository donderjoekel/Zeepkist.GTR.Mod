using Microsoft.Extensions.Logging;
using TMPro;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Patching.Patches;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.UI.Totw;

public class TotwMenuButtonService : IEagerService
{
    private const string TotwSlotName = "TOTW";
    private const string TotwButtonName = "Track of the Week";
    private const string OnlineButtonName = "Zeepkist Online";
    private const string SplitscreenButtonName = "Splitscreen";
    private const string FreePlayButtonName = "FreePlay";
    private const string GoBackButtonName = "Go Back";
    private const string PlayersIconName = "Players Icon";

    private readonly ILogger<TotwMenuButtonService> _logger;

    public TotwMenuButtonService(ILogger<TotwMenuButtonService> logger)
    {
        _logger = logger;
        MainMenuUi_Awake.Postfixed += OnMainMenuAwake;
    }

    private void OnMainMenuAwake()
    {
        StartGameUI startGameUi = Object.FindObjectOfType<StartGameUI>(true);
        if (startGameUi == null)
        {
            _logger.LogWarning("StartGameUI not found; skipping TOTW button injection");
            return;
        }

        if (FindChildByName(startGameUi.transform, TotwButtonName) != null)
            return;

        Transform onlineButtonTransform = FindChildByName(startGameUi.transform, OnlineButtonName);
        if (onlineButtonTransform == null)
        {
            _logger.LogWarning("{Button} not found; skipping TOTW button injection", OnlineButtonName);
            return;
        }

        RectTransform onlineSlot = onlineButtonTransform.parent as RectTransform;
        if (onlineSlot == null)
        {
            _logger.LogWarning("Online button has no RectTransform parent slot");
            return;
        }

        Transform panel = onlineSlot.parent;
        if (panel == null)
        {
            _logger.LogWarning("Online slot has no panel parent");
            return;
        }

        ShrinkOnlineSlot(onlineSlot);

        GameObject totwSlotObject = Object.Instantiate(onlineSlot.gameObject, panel);
        totwSlotObject.name = TotwSlotName;
        RectTransform totwSlot = totwSlotObject.GetComponent<RectTransform>();
        SetHalfWidthAnchors(totwSlot, rightHalf: true);

        Transform totwButtonTransform = FindChildByName(totwSlot, OnlineButtonName);
        if (totwButtonTransform == null)
            totwButtonTransform = totwSlot.GetComponentInChildren<GenericButton>(true)?.transform;

        if (totwButtonTransform == null)
        {
            _logger.LogError("Failed to find cloned TOTW button");
            Object.Destroy(totwSlotObject);
            return;
        }

        totwButtonTransform.name = TotwButtonName;
        ConfigureTotwButton(totwButtonTransform.gameObject, onlineButtonTransform.GetComponent<GenericButton>(),
            startGameUi);
    }

    private static void ShrinkOnlineSlot(RectTransform onlineSlot)
    {
        SetHalfWidthAnchors(onlineSlot, rightHalf: false);
    }

    private static void SetHalfWidthAnchors(RectTransform slot, bool rightHalf)
    {
        float yMin = slot.anchorMin.y;
        float yMax = slot.anchorMax.y;
        if (rightHalf)
        {
            slot.anchorMin = new Vector2(0.525f, yMin);
            slot.anchorMax = new Vector2(1f, yMax);
        }
        else
        {
            slot.anchorMin = new Vector2(0f, yMin);
            slot.anchorMax = new Vector2(0.475f, yMax);
        }

        slot.anchoredPosition = Vector2.zero;
        slot.sizeDelta = Vector2.zero;
    }

    private void ConfigureTotwButton(GameObject totwButtonObject, GenericButton onlineButton, StartGameUI startGameUi)
    {
        DisableButtonIfSteamNotConnected steamGate =
            totwButtonObject.GetComponent<DisableButtonIfSteamNotConnected>();
        if (steamGate != null)
            Object.Destroy(steamGate);

        foreach (TMP_Text text in totwButtonObject.GetComponentsInChildren<TMP_Text>(true))
            text.text = TotwButtonName;

        ApplyStarIcon(totwButtonObject);

        GenericButton totwButton = totwButtonObject.GetComponent<GenericButton>();
        if (totwButton == null)
        {
            _logger.LogError("Cloned TOTW object has no GenericButton");
            return;
        }

        totwButton.onClick = new UnityEvent();
        totwButton.onClick.AddListener(OnTotwClicked);
        totwButton.disabled = false;

        WireNavigation(totwButton, onlineButton, startGameUi);

        if (startGameUi.buttonsToDisableWhenGoingIntoAthing != null &&
            !startGameUi.buttonsToDisableWhenGoingIntoAthing.Contains(totwButton))
        {
            startGameUi.buttonsToDisableWhenGoingIntoAthing.Add(totwButton);
        }

        _logger.LogInformation("Injected Track of the Week button into StartGameUI");
    }

    private void WireNavigation(GenericButton totwButton, GenericButton onlineButton, StartGameUI startGameUi)
    {
        GenericButton splitscreen = FindButton(startGameUi.transform, SplitscreenButtonName);
        GenericButton freePlay = FindButton(startGameUi.transform, FreePlayButtonName);
        GenericButton goBack = FindButton(startGameUi.transform, GoBackButtonName);

        GenericButton previousOnlineDown = onlineButton.down;
        GenericButton previousOnlineRight = onlineButton.right;
        GenericButton previousOnlineUp = onlineButton.up;

        onlineButton.right = totwButton;
        onlineButton.down = splitscreen != null ? splitscreen : previousOnlineDown;

        totwButton.left = onlineButton;
        totwButton.right = goBack != null ? goBack : previousOnlineRight;
        totwButton.up = previousOnlineUp;
        totwButton.down = freePlay != null ? freePlay : previousOnlineDown;

        if (splitscreen != null)
            splitscreen.up = onlineButton;

        if (freePlay != null)
            freePlay.up = totwButton;

        if (goBack != null && goBack.up == onlineButton)
            goBack.up = totwButton;
    }

    private void OnTotwClicked()
    {
        _logger.LogInformation("Track of the Week clicked (stub)");
    }

    private void ApplyStarIcon(GameObject totwButtonObject)
    {
        Sprite starSprite = PlayerManager.Instance != null ? PlayerManager.Instance.youTriedMedal : null;
        if (starSprite == null)
        {
            _logger.LogWarning("youTriedMedal sprite not available; leaving cloned Online icon");
            return;
        }

        Transform iconTransform = FindChildByName(totwButtonObject.transform, PlayersIconName);
        Image iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        if (iconImage == null)
        {
            _logger.LogWarning("{Icon} Image not found on TOTW button", PlayersIconName);
            return;
        }

        iconImage.sprite = starSprite;
        iconImage.preserveAspect = true;
    }

    private static GenericButton FindButton(Transform root, string name)
    {
        Transform transform = FindChildByName(root, name);
        return transform != null ? transform.GetComponent<GenericButton>() : null;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
