using TNRD.Zeepkist.GTR.Mod.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZeepkistClient;

namespace TNRD.Zeepkist.GTR.Mod.Components;

public class ShortcutsHandler : MonoBehaviour
{
    private bool isRacingOffline;
    private OnlineChatUI currentChatUI;
    private PauseHandler currentPauseHandler;

    private void Awake()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        OnlineChatUI_Awake.Awake += OnChatUIAwake;
        PauseHandler_Awake.Awake += OnPauseHandlerAwake;
    }

    private void OnActiveSceneChanged(Scene from, Scene to)
    {
        isRacingOffline = to.name == "GameScene";
    }

    private void OnDestroy()
    {
        OnlineChatUI_Awake.Awake -= OnChatUIAwake;
        PauseHandler_Awake.Awake -= OnPauseHandlerAwake;
    }

    private void OnChatUIAwake(OnlineChatUI onlineChatUI)
    {
        currentChatUI = onlineChatUI;
    }

    private void OnPauseHandlerAwake(PauseHandler pauseHandler)
    {
        currentPauseHandler = pauseHandler;
    }

    private void Update()
    {
        if (ZeepkistNetwork.IsConnected && ZeepkistNetwork.IsConnectedToGame)
        {
            if (currentChatUI == null || currentChatUI.GetChatLocked())
                return;

            if (currentPauseHandler == null || currentPauseHandler.IsPaused)
                return;

            HandleShortcuts();
        }
        else if (isRacingOffline)
        {
            HandleShortcuts();
        }
    }

    private void HandleShortcuts()
    {
        if (Input.GetKeyDown(Plugin.ConfigToggleEnableRecords.Value))
        {
            Plugin.ConfigEnableRecords.Value = !Plugin.ConfigEnableRecords.Value;

            PlayerManager.Instance.messenger.Log(Plugin.ConfigEnableRecords.Value
                    ? "Enabled Records"
                    : "Disabled Records",
                2.5f);
        }

        if (Input.GetKeyDown(Plugin.ConfigToggleEnableGhosts.Value))
        {
            Plugin.ConfigEnableGhosts.Value = !Plugin.ConfigEnableGhosts.Value;

            PlayerManager.Instance.messenger.Log(Plugin.ConfigEnableGhosts.Value
                    ? "Enabled Ghosts"
                    : "Disabled Ghosts",
                2.5f);
        }

        if (Input.GetKeyDown(Plugin.ConfigToggleShowGhosts.Value))
        {
            Plugin.ConfigShowGhosts.Value = !Plugin.ConfigShowGhosts.Value;

            PlayerManager.Instance.messenger.Log(Plugin.ConfigShowGhosts.Value
                    ? "Showing Ghosts"
                    : "Hiding Ghosts",
                2.5f);
        }

        if (Input.GetKeyDown(Plugin.ConfigToggleShowGhostNames.Value))
        {
            Plugin.ConfigShowGhostNames.Value = !Plugin.ConfigShowGhostNames.Value;

            PlayerManager.Instance.messenger.Log(Plugin.ConfigShowGhostNames.Value
                    ? "Showing Ghost Names"
                    : "Hiding Ghost Names",
                2.5f);
        }

        if (Input.GetKeyDown(Plugin.ConfigToggleShowRecordSetMessage.Value))
        {
            Plugin.ConfigShowRecordSetMessage.Value = !Plugin.ConfigShowRecordSetMessage.Value;

            PlayerManager.Instance.messenger.Log(Plugin.ConfigShowRecordSetMessage.Value
                    ? "Showing Record Set Message"
                    : "Hiding Record Set Message",
                2.5f);
        }

        if (Input.GetKeyDown(Plugin.ConfigToggleShowGhostTransparent.Value))
        {
            Plugin.ConfigShowGhostTransparent.Value = !Plugin.ConfigShowGhostTransparent.Value;

            PlayerManager.Instance.messenger.Log(Plugin.ConfigShowGhostTransparent.Value
                    ? "Transparent ghosts!"
                    : "Opaque ghosts!",
                2.5f);
        }
    }
}
