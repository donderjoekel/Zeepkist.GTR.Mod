using Microsoft.Extensions.Logging;
using TMPro;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Patching.Patches;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.UI.Tournaments;

public class TournamentsMenuButtonService : IEagerService
{
    private const string TournamentsSlotName = "Tournaments";
    private const string TournamentsButtonName = "Tournaments";
    private const string OnlineButtonName = "Zeepkist Online";
    private const string SplitscreenButtonName = "Splitscreen";
    private const string FreePlayButtonName = "FreePlay";
    private const string GoBackButtonName = "Go Back";
    private const string PlayersIconName = "Players Icon";

    private readonly ILogger<TournamentsMenuButtonService> _logger;
    private readonly TournamentsWindowState _windowState;

    public TournamentsMenuButtonService(
        ILogger<TournamentsMenuButtonService> logger,
        TournamentsWindowState windowState)
    {
        _logger = logger;
        _windowState = windowState;
        MainMenuUi_Awake.Postfixed += OnMainMenuAwake;
    }

    private void OnMainMenuAwake()
    {
        StartGameUI startGameUi = Object.FindObjectOfType<StartGameUI>(true);
        if (startGameUi == null)
        {
            _logger.LogWarning("StartGameUI not found; skipping Tournaments button injection");
            return;
        }

        if (FindChildByName(startGameUi.transform, TournamentsButtonName) != null)
            return;

        Transform onlineButtonTransform = FindChildByName(startGameUi.transform, OnlineButtonName);
        if (onlineButtonTransform == null)
        {
            _logger.LogWarning("{Button} not found; skipping Tournaments button injection", OnlineButtonName);
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

        GameObject tournamentsSlotObject = Object.Instantiate(onlineSlot.gameObject, panel);
        tournamentsSlotObject.name = TournamentsSlotName;
        RectTransform tournamentsSlot = tournamentsSlotObject.GetComponent<RectTransform>();
        SetHalfWidthAnchors(tournamentsSlot, rightHalf: true);

        Transform tournamentsButtonTransform = FindChildByName(tournamentsSlot, OnlineButtonName);
        if (tournamentsButtonTransform == null)
            tournamentsButtonTransform = tournamentsSlot.GetComponentInChildren<GenericButton>(true)?.transform;

        if (tournamentsButtonTransform == null)
        {
            _logger.LogError("Failed to find cloned Tournaments button");
            Object.Destroy(tournamentsSlotObject);
            return;
        }

        tournamentsButtonTransform.name = TournamentsButtonName;
        ConfigureTournamentsButton(
            tournamentsButtonTransform.gameObject,
            onlineButtonTransform.GetComponent<GenericButton>(),
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

    private void ConfigureTournamentsButton(
        GameObject tournamentsButtonObject,
        GenericButton onlineButton,
        StartGameUI startGameUi)
    {
        DisableButtonIfSteamNotConnected steamGate =
            tournamentsButtonObject.GetComponent<DisableButtonIfSteamNotConnected>();
        if (steamGate != null)
            Object.Destroy(steamGate);

        foreach (TMP_Text text in tournamentsButtonObject.GetComponentsInChildren<TMP_Text>(true))
            text.text = TournamentsButtonName;

        ApplyStarIcon(tournamentsButtonObject);

        GenericButton tournamentsButton = tournamentsButtonObject.GetComponent<GenericButton>();
        if (tournamentsButton == null)
        {
            _logger.LogError("Cloned Tournaments object has no GenericButton");
            return;
        }

        tournamentsButton.onClick = new UnityEvent();
        tournamentsButton.onClick.AddListener(OnTournamentsClicked);
        tournamentsButton.disabled = false;

        WireNavigation(tournamentsButton, onlineButton, startGameUi);

        if (startGameUi.buttonsToDisableWhenGoingIntoAthing != null &&
            !startGameUi.buttonsToDisableWhenGoingIntoAthing.Contains(tournamentsButton))
        {
            startGameUi.buttonsToDisableWhenGoingIntoAthing.Add(tournamentsButton);
        }

        _logger.LogInformation("Injected Tournaments button into StartGameUI");
    }

    private void WireNavigation(
        GenericButton tournamentsButton,
        GenericButton onlineButton,
        StartGameUI startGameUi)
    {
        GenericButton splitscreen = FindButton(startGameUi.transform, SplitscreenButtonName);
        GenericButton freePlay = FindButton(startGameUi.transform, FreePlayButtonName);
        GenericButton goBack = FindButton(startGameUi.transform, GoBackButtonName);

        GenericButton previousOnlineDown = onlineButton.down;
        GenericButton previousOnlineRight = onlineButton.right;
        GenericButton previousOnlineUp = onlineButton.up;

        onlineButton.right = tournamentsButton;
        onlineButton.down = splitscreen != null ? splitscreen : previousOnlineDown;

        tournamentsButton.left = onlineButton;
        tournamentsButton.right = goBack != null ? goBack : previousOnlineRight;
        tournamentsButton.up = previousOnlineUp;
        tournamentsButton.down = freePlay != null ? freePlay : previousOnlineDown;

        if (splitscreen != null)
            splitscreen.up = onlineButton;

        if (freePlay != null)
            freePlay.up = tournamentsButton;

        if (goBack != null && goBack.up == onlineButton)
            goBack.up = tournamentsButton;
    }

    private void OnTournamentsClicked()
    {
        _windowState.Open();
    }

    private void ApplyStarIcon(GameObject tournamentsButtonObject)
    {
        Sprite starSprite = PlayerManager.Instance != null ? PlayerManager.Instance.youTriedMedal : null;
        if (starSprite == null)
        {
            _logger.LogWarning("youTriedMedal sprite not available; leaving cloned Online icon");
            return;
        }

        Transform iconTransform = FindChildByName(tournamentsButtonObject.transform, PlayersIconName);
        Image iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        if (iconImage == null)
        {
            _logger.LogWarning("{Icon} Image not found on Tournaments button", PlayersIconName);
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
