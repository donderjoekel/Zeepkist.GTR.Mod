using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.Mod.Api.Users;
using TNRD.Zeepkist.GTR.Mod.Components;
using TNRD.Zeepkist.GTR.Mod.Components.Ghosting;
using TNRD.Zeepkist.GTR.Mod.Components.Leaderboard.Pages;
using TNRD.Zeepkist.GTR.Mod.Patches;
using UnityEngine;
using ZeepSDK.Leaderboard;
using Result = TNRD.Zeepkist.GTR.FluentResults.Result;

namespace TNRD.Zeepkist.GTR.Mod;

internal class Plugin : MonoBehaviour
{
    private Harmony harmony;

    public static ConfigEntry<bool> ConfigEnableRecords;
    public static ConfigEntry<bool> ConfigEnableGhosts;
    public static ConfigEntry<bool> ConfigEnableVoting;

    public static ConfigEntry<bool> ConfigShowGhosts;
    public static ConfigEntry<bool> ConfigShowGhostNames;
    public static ConfigEntry<bool> ConfigShowRecordSetMessage;
    public static ConfigEntry<bool> ConfigShowWorldRecordHolder;

    public static ConfigEntry<KeyCode> ConfigToggleEnableGhosts;
    public static ConfigEntry<KeyCode> ConfigToggleEnableRecords;
    public static ConfigEntry<KeyCode> ConfigToggleShowGhosts;
    public static ConfigEntry<KeyCode> ConfigToggleShowGhostNames;
    public static ConfigEntry<KeyCode> ConfigToggleShowRecordSetMessage;
    public static ConfigEntry<KeyCode> ConfigToggleShowWorldRecordHolder;

    public static ConfigEntry<bool> ConfigShowOfflineWorldRecord;
    public static ConfigEntry<bool> ConfigShowOfflinePersonalBest;
    public static ConfigEntry<bool> ConfigShowOfflineAuthorMedal;
    public static ConfigEntry<bool> ConfigShowOfflineGoldMedal;
    public static ConfigEntry<bool> ConfigShowOfflineSilverMedal;
    public static ConfigEntry<bool> ConfigShowOfflineBronzeMedal;
    public static ConfigEntry<string> ConfigOfflineGhostMode;
    public static ConfigEntry<int> ConfigOfflineGhostCount;

    public static ConfigEntry<string> ConfigAuthUrl;
    public static ConfigEntry<string> ConfigApiUrl;

    public static AssetBundle AssetBundle { get; private set; }

    public ConfigFile Config { get; set; }
    public ManualLogSource Logger { get; set; }
    public PluginInfo Info { get; set; }

    private void Start()
    {
        if (!LoadUIAssembly())
        {
            Logger.LogFatal("Unable to load UI assembly");
            return;
        }

        if (!LoadAssetBundle())
        {
            Logger.LogFatal("Unable to load asset bundle");
        }

        SetupConfig();

        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();

        gameObject.AddComponent<GhostRecorder>();
        gameObject.AddComponent<LevelCreator>();
        gameObject.AddComponent<ResultScreenshotter>();
        gameObject.AddComponent<RecordSubmitter>();
        gameObject.AddComponent<OnlineGhostLoader>();
        gameObject.AddComponent<OfflineGhostLoader>();
        gameObject.AddComponent<ShortcutsHandler>();
        gameObject.AddComponent<RatingPopupHandler>();
        gameObject.AddComponent<WorldRecordHolderUi>();

        LeaderboardApi.AddTab<GtrLeaderboardTab>();

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        MainMenuUi_Awake.Awake += MainMenuUiOnAwake;
    }

    private bool LoadUIAssembly()
    {
        string dir = Path.GetDirectoryName(Info.Location);
        string assemblyPath = dir + "/TNRD.Zeepkist.GTR.UI.dll";
        try
        {
            Assembly asm = Assembly.LoadFile(assemblyPath);
            return asm != null;
        }
        catch (Exception e)
        {
            Logger.LogFatal(e.Message);
            return false;
        }
    }

    private bool LoadAssetBundle()
    {
        string dir = Path.GetDirectoryName(Info.Location);
        string assetBundlePath = dir + "/gtr-ui";
        try
        {
            AssetBundle = AssetBundle.LoadFromFile(assetBundlePath);
            return AssetBundle != null;
        }
        catch (Exception e)
        {
            Logger.LogFatal(e.Message);
            return false;
        }
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
        harmony = null;

        MainMenuUi_Awake.Awake -= MainMenuUiOnAwake;
    }

    private void SetupConfig()
    {
        ConfigEnableRecords = Config.Bind("General", "Enable Records", true, "Should records be tracked");
        ConfigEnableGhosts = Config.Bind("General", "Enable Ghosts", true, "Should ghosts be enabled");
        ConfigEnableVoting = Config.Bind("General", "Enable Voting", false, "Should voting be enabled");

        ConfigShowGhosts = Config.Bind("Visibility", "Show Ghosts", true, "Should ghosts be shown");
        ConfigShowGhostNames = Config.Bind("Visibility", "Show Ghost Names", true, "Should ghost names be shown");
        ConfigShowRecordSetMessage = Config.Bind("Visibility",
            "Show Record Set Message",
            true,
            "Should the record set message be shown");
        ConfigShowWorldRecordHolder = Config.Bind("Visibility",
            "Show World Record Holder",
            true,
            "Should the world record holder be shown");

        ConfigToggleEnableRecords = Config.Bind("Keys",
            "Toggle Enable Records",
            KeyCode.None,
            "Toggles if records should be enabled");

        ConfigToggleEnableGhosts = Config.Bind("Keys",
            "Toggle Enable Ghosts",
            KeyCode.None,
            "Toggles if ghosts should be enabled");

        ConfigToggleShowGhosts = Config.Bind("Keys",
            "Toggle Ghost Model Visibility",
            KeyCode.None,
            "Toggles the ghost visibility");
        ConfigToggleShowGhostNames = Config.Bind("Keys",
            "Toggle Ghost Name Visibility",
            KeyCode.None,
            "Toggles the ghost name visibility");

        ConfigToggleShowRecordSetMessage = Config.Bind("Keys",
            "Toggle Record Set Message Visibility",
            KeyCode.None,
            "Toggles the record set message visibility");

        ConfigToggleShowWorldRecordHolder = Config.Bind("Keys",
            "Toggle World Record Holder Visibility",
            KeyCode.None,
            "Toggles the world record holder visibility");

        ConfigApiUrl = Config.Bind("URLs",
            "The API address",
            SDK.Sdk.DEFAULT_API_ADDRESS,
            "Allows you to set a custom API address");

        ConfigAuthUrl = Config.Bind("URLs",
            "The Auth address",
            SDK.Sdk.DEFAULT_AUTH_ADDRESS,
            "Allows you to set a custom Auth address");

        SetupGhostsConfig();
    }

    private void SetupGhostsConfig()
    {
        ConfigShowOfflineWorldRecord = Config.Bind("Ghosts (Offline)",
            "Show World Record",
            true,
            "Should the world record ghost be shown");

        ConfigShowOfflinePersonalBest = Config.Bind("Ghosts (Offline)",
            "Show Personal Best",
            true,
            "Should the personal best ghost be shown");

        ConfigShowOfflineAuthorMedal = Config.Bind("Ghosts (Offline)",
            "Show Author Medal",
            true,
            "Should the author medal ghost be shown");

        ConfigShowOfflineGoldMedal = Config.Bind("Ghosts (Offline)",
            "Show Gold Medal",
            true,
            "Should the gold medal ghost be shown");

        ConfigShowOfflineSilverMedal = Config.Bind("Ghosts (Offline)",
            "Show Silver Medal",
            true,
            "Should the silver medal ghost be shown");

        ConfigShowOfflineBronzeMedal = Config.Bind("Ghosts (Offline)",
            "Show Bronze Medal",
            true,
            "Should the bronze medal ghost be shown");

        ConfigOfflineGhostMode = Config.Bind("Ghosts (Offline)",
            "Ghost Mode",
            GhostMode.OFF,
            new ConfigDescription("The ghost mode to use",
                new AcceptableValueList<string>(GhostMode.OFF, GhostMode.RANDOM, GhostMode.TOP)));

        ConfigOfflineGhostCount = Config.Bind("Ghosts (Offline)",
            "Ghost Count",
            10,
            "The amount of ghosts to show based on the ghost mode");
    }

    private void MainMenuUiOnAwake()
    {
        Login().Forget();
    }

    private async UniTaskVoid Login()
    {
        Logger.LogInfo("Waiting for SteamClient to become valid");
        while (!SteamClient.IsValid)
        {
            await UniTask.Yield();
        }

        Logger.LogInfo("Waiting for SteamUser to log in");
        while (!SteamClient.IsLoggedOn)
        {
            await UniTask.Yield();
        }

        Result result = await AttemptLogin();
        if (result.IsSuccess)
        {
            PlayerManager.Instance.messenger.Log("[GTR] Logged in", 2.5f);
            Result updateDiscordId = await CustomUsersApi.UpdateDiscordId();
        }
        else
        {
            await UniTask.Delay(TimeSpan.FromSeconds(1));
            PlayerManager.Instance.messenger.LogError("[GTR] Failed to log in", 2.5f);
            Logger.LogError($"Failed to log in: {result}");
        }
    }

    private async UniTask<Result> AttemptLogin()
    {
        Logger.LogInfo("Logging in to GTR");
        Result result = await Sdk.Instance.UsersApi.Login(MyPluginInfo.PLUGIN_VERSION);
        if (result.IsSuccess)
            return result;

        Logger.LogWarning("Failed so waiting 5 seconds for another attempt");
        await UniTask.Delay(TimeSpan.FromSeconds(5));
        result = await Sdk.Instance.UsersApi.Login(MyPluginInfo.PLUGIN_VERSION);
        if (result.IsSuccess)
            return result;

        Logger.LogWarning("Failed so waiting 10 seconds for another attempt");
        await UniTask.Delay(TimeSpan.FromSeconds(10));
        return await Sdk.Instance.UsersApi.Login(MyPluginInfo.PLUGIN_VERSION);
    }
}
