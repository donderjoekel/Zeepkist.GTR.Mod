using BepInEx.Configuration;
using TNRD.Zeepkist.GTR.Core;
using UnityEngine;
using ZeepSDK.Settings;

namespace TNRD.Zeepkist.GTR.Configuration;

public class ConfigService : IEagerService
{
    public const string ProductionBackendUrl = "https://backend.zeepki.st";
    public const string LocalDevelopmentBackendUrl = "http://localhost:3000";
    public const string CdnUrl = "https://cdn.zeepki.st";
    public const string ProductionGraphQLUrl = "https://graphql.zeepki.st";
    public const string LocalDevelopmentGraphQLUrl = "http://127.0.0.1:5000/";

    public ConfigEntry<bool> SubmitRecords { get; private set; }
    public ConfigEntry<bool> ShowRecordSubmitMessage { get; private set; }
    public ConfigEntry<float> ShowRecordSubmitMessageDuration { get; private set; }

    public ConfigEntry<bool> EnableGhosts { get; private set; }

    public ConfigEntry<bool> ShowGhosts { get; private set; }
    public ConfigEntry<bool> ShowGhostNames { get; private set; }
    public ConfigEntry<bool> ShowGhostTransparent { get; private set; }
    public ConfigEntry<bool> ShowGlobalPersonalBest { get; private set; }

    public ConfigEntry<int> MaximumVisibleOfflineGhosts { get; private set; }
    public ConfigEntry<int> MaximumVisibleTopRecordGhosts { get; private set; }
    public ConfigEntry<int> MaximumGhostColours { get; private set; }

    public ConfigEntry<KeyCode> ToggleEnableGhosts { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowGhosts { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowGhostNames { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowGhostTransparent { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowGlobalPersonalBest { get; private set; }

    public ConfigEntry<bool> ShowRecordHolder { get; private set; }
    public ConfigEntry<bool> ShowWorldRecordOnHolder { get; private set; }
    public ConfigEntry<bool> ShowPersonalBestOnHolder { get; private set; }
    public ConfigEntry<bool> ShowWorldRecordHolder { get; private set; }
    public ConfigEntry<bool> ShowPersonalBestHolder { get; private set; }

    public ConfigEntry<float> RecordHolderSwitchTime { get; private set; }

    public ConfigEntry<KeyCode> ToggleShowRecordHolder { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowWorldRecordOnHolder { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowPersonalBestOnHolder { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowWorldRecordHolder { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowPersonalBestHolder { get; private set; }

    public ConfigEntry<bool> ButtonLinkDiscord { get; private set; }
    public ConfigEntry<bool> ButtonUnlinkDiscord { get; private set; }

    public ConfigEntry<bool> UseLocalDevelopmentBackend { get; private set; }
    public ConfigEntry<bool> UseLocalDevelopmentGraphQL { get; private set; }

    public string SelectedBackendUrl => UseLocalDevelopmentBackend.Value
        ? LocalDevelopmentBackendUrl
        : ProductionBackendUrl;

    public string SelectedGraphQLUrl => UseLocalDevelopmentGraphQL.Value
        ? LocalDevelopmentGraphQLUrl
        : ProductionGraphQLUrl;

    public ConfigEntry<KeyCode> ToggleCursorEnabled { get; private set; }

    public ConfigEntry<KeyCode> PhotoModeCameraFreezeKey { get; private set; }

    public ConfigEntry<float> SpectateFirstPersonEyeHeight { get; private set; }
    public ConfigEntry<float> SpectateThirdPersonDistance { get; private set; }
    public ConfigEntry<float> SpectateThirdPersonHeight { get; private set; }
    public ConfigEntry<float> SpectateThirdPersonLookHeight { get; private set; }
    public ConfigEntry<float> SpectateThirdPersonSmoothTime { get; private set; }
    public ConfigEntry<float> SpectateTopDownHeight { get; private set; }
    public ConfigEntry<float> SpectateTopDownPitch { get; private set; }

    public ConfigService(ConfigFile config, Plugin plugin)
    {
        ConfigRecords(config);
        ConfigGhosts(config);
        ConfigRecordHolder(config);
        ConfigDiscord(config);
        ConfigUrls(config);
        ConfigDebug(config);
        ConfigPlayback(config);

        SettingsApi.ConfigureModSettingsTabs(plugin, builder =>
        {
            builder.Tab("Records",
                "1. Records - General");
            builder.Tab("Ghosts",
                "2. Ghosts - General",
                "2.1 Ghosts - Visibility",
                "2.2 Ghosts - Keys",
                "2.3 - Ghosts - Offline Ghosts");
            builder.Tab("Record Holder",
                "3. Record Holder - General",
                "3.1 Record Holder - Visibility",
                "3.2 Record Holder - Keys");
            builder.Tab("Other",
                "4. Discord",
                "5. URLs",
                "6. Debug",
                "7. Playback");
        });
    }

    private void ConfigRecords(ConfigFile config)
    {
        SubmitRecords = config.Bind(
            "1. Records - General",
            "1. Submit Records",
            true,
            "Should records be submitted");
        ShowRecordSubmitMessage = config.Bind(
            "1. Records - General",
            "3. Show Record Submit Message",
            true,
            "Should the record submit message be shown");
        ShowRecordSubmitMessageDuration = config.Bind(
            "1. Records - General",
            "4. Show Record Submit Message Duration",
            2.5f,
            "The duration in seconds that the record submit message should be shown for");
    }

    private void ConfigGhosts(ConfigFile config)
    {
        EnableGhosts = config.Bind(
            "2. Ghosts - General",
            "1. Enable Ghosts",
            true,
            "Should ghosts be enabled\n" +
            "This completely disables anything to do with fetching and displaying ghosts");

        ShowGhosts = config.Bind(
            "2.1 Ghosts - Visibility",
            "1. Show Ghosts",
            true,
            "Should ghosts be shown");
        ShowGhostNames = config.Bind(
            "2.1 Ghosts - Visibility",
            "2. Show Ghost Names",
            true,
            "Should ghost names be shown");
        ShowGhostTransparent = config.Bind(
            "2.1 Ghosts - Visibility",
            "3. Show Ghost Transparent",
            true,
            "Should ghosts be transparent");
        ShowGlobalPersonalBest = config.Bind(
            "2.1 Ghosts - Visibility",
            "4. Show Global Personal Best",
            true,
            "Should the global personal best be shown");

        ToggleEnableGhosts = config.Bind(
            "2.2 Ghosts - Keys",
            "1. Toggle Enable Ghosts",
            KeyCode.None,
            "Toggles if ghosts should be enabled");
        ToggleShowGhosts = config.Bind(
            "2.2 Ghosts - Keys",
            "2. Toggle Show Ghosts",
            KeyCode.None,
            "Toggles if ghosts should be shown");
        ToggleShowGhostNames = config.Bind(
            "2.2 Ghosts - Keys",
            "3. Toggle Show Ghost Names",
            KeyCode.None,
            "Toggles if ghost names should be shown");
        ToggleShowGhostTransparent = config.Bind(
            "2.2 Ghosts - Keys",
            "4. Toggle Show Ghost Transparent",
            KeyCode.None,
            "Toggles if ghosts should be transparent");
        ToggleShowGlobalPersonalBest = config.Bind(
            "2.2 Ghosts - Keys",
            "5. Toggle Show Global Personal Best",
            KeyCode.None,
            "Toggles if the global personal best should be shown");

        // TODO: Replace max 1000 with int.MaxValue once pagination added
        MaximumVisibleOfflineGhosts = config.Bind(
            "2.3 - Ghosts - Offline Ghosts",
            "1. Personal Best Ghosts",
            -1,
            new ConfigDescription(
                "(-1 means Show All) Maximum number of fastest PB ghosts loaded",
                new AcceptableValueRange<int>(-1, 1000)
            )
        );
        // TODO: Replace max 1000 with int.MaxValue once pagination added
        MaximumVisibleTopRecordGhosts = config.Bind(
            "2.3 - Ghosts - Offline Ghosts",
            "2. Top Record Ghosts",
            500,
            new ConfigDescription(
                "Maximum number of fastest top record ghosts loaded",
                new AcceptableValueRange<int>(1, 1000)
            )
        );
        MaximumGhostColours = config.Bind(
            "2.3 - Ghosts - Offline Ghosts",
            "3. Max Ghost Colours",
            24,
            new ConfigDescription(
                "Maximum number of ghost colours to show.\n\n" +
                "Increasing this can reduce performance.\n\n" +
                "Set to 1 for maximum performance, but all\n" +
                "ghosts will be the same colour.",
                new AcceptableValueRange<int>(1, 128)
            )
        );

    }

    private void ConfigRecordHolder(ConfigFile config)
    {
        RecordHolderSwitchTime = config.Bind(
            "3. Record Holder - General",
            "1. Record Holder Switch Time",
            10f,
            "The time in seconds it takes to switch between the world record and personal best");

        ShowRecordHolder = config.Bind(
            "3.1 Record Holder - Visibility",
            "1. Show Record Holder",
            true,
            "Should the record holder be shown");
        ShowWorldRecordOnHolder = config.Bind(
            "3.1 Record Holder - Visibility",
            "2. Show World Record On Holder",
            true,
            "Should the world record be shown on the record holder");
        ShowPersonalBestOnHolder = config.Bind(
            "3.1 Record Holder - Visibility",
            "3. Show Personal Best On Holder",
            true,
            "Should the personal best be shown on the record holder");
        ShowWorldRecordHolder = config.Bind(
            "3.1 Record Holder - Visibility",
            "4. Show World Record Holder",
            false,
            "Should the individual world record holder be shown");
        ShowPersonalBestHolder = config.Bind(
            "3.1 Record Holder - Visibility",
            "5. Show Personal Best Holder",
            false,
            "Should the individual personal best holder be shown");

        ToggleShowRecordHolder = config.Bind(
            "3.2 Record Holder - Keys",
            "1. Toggle Show Record Holder",
            KeyCode.None,
            "Toggles if the record holder should be shown");
        ToggleShowWorldRecordOnHolder = config.Bind(
            "3.2 Record Holder - Keys",
            "2. Toggle Show World Record On Holder",
            KeyCode.None,
            "Toggles if the world record should be shown on the record holder");
        ToggleShowPersonalBestOnHolder = config.Bind(
            "3.2 Record Holder - Keys",
            "3. Toggle Show Personal Best On Holder",
            KeyCode.None,
            "Toggles if the personal best should be shown on the record holder");
        ToggleShowWorldRecordHolder = config.Bind(
            "3.2 Record Holder - Keys",
            "4. Toggle Show World Record Holder",
            KeyCode.None,
            "Toggles if the individual world record holder should be shown");
        ToggleShowPersonalBestHolder = config.Bind(
            "3.2 Record Holder - Keys",
            "5. Toggle Show Personal Best Holder",
            KeyCode.None,
            "Toggles if the individual personal best holder should be shown");
    }

    private void ConfigDiscord(ConfigFile config)
    {
        ButtonLinkDiscord = config.Bind(
            "4. Discord",
            "Link",
            true,
            "[Button] Link your signed in discord account to your GTR user");
        ButtonUnlinkDiscord = config.Bind(
            "4. Discord",
            "Unlink",
            true,
            "[Button] Unlink your signed in discord account from your GTR user");
    }

    private void ConfigUrls(ConfigFile config)
    {
        UseLocalDevelopmentBackend = config.Bind(
            "5. URLs",
            "Local Backend",
            false,
            "Use http://localhost:3000 instead of production backend\n" +
            "Changing this logs in again against the selected backend");

        UseLocalDevelopmentGraphQL = config.Bind(
            "5. URLs",
            "Local GraphQL",
            false,
            "Use http://127.0.0.1:5000/ instead of production GraphQL");
    }

    private void ConfigDebug(ConfigFile config)
    {
        ToggleCursorEnabled = config.Bind(
            "6. Debug",
            "1. Toggle Cursor Enabled",
            KeyCode.None,
            "Toggles UnityEngine.Cursor.visible and unlocks the cursor when shown");
    }

    private void ConfigPlayback(ConfigFile config)
    {
        PhotoModeCameraFreezeKey = config.Bind(
            "7. Playback",
            "1. Camera Freeze Key",
            KeyCode.None,
            "Hold this key in photo mode to freeze the flying camera while using the playback UI\n" +
            "Set to None to disable");
        SpectateFirstPersonEyeHeight = config.Bind(
            "7. Playback",
            "2. First Person Eye Height",
            1.3f,
            "Vertical offset above the ghost for first-person spectate camera");
        SpectateThirdPersonDistance = config.Bind(
            "7. Playback",
            "3. Third Person Distance",
            6f,
            "Distance behind the ghost for third-person spectate camera");
        SpectateThirdPersonHeight = config.Bind(
            "7. Playback",
            "4. Third Person Height",
            2.5f,
            "Vertical offset above the ghost for third-person spectate camera");
        SpectateThirdPersonLookHeight = config.Bind(
            "7. Playback",
            "5. Third Person Look Height",
            1f,
            "Vertical offset on the ghost that third-person camera looks at");
        SpectateThirdPersonSmoothTime = config.Bind(
            "7. Playback",
            "6. Third Person Smooth Time",
            0.12f,
            "Smoothing time in seconds for smooth third-person spectate camera");
        SpectateTopDownHeight = config.Bind(
            "7. Playback",
            "7. Top Down Height",
            25f,
            "Height above the ghost for top-down spectate camera");
        SpectateTopDownPitch = config.Bind(
            "7. Playback",
            "8. Top Down Pitch",
            20f,
            "Pitch angle in degrees for top-down spectate camera\n" +
            "0 = straight down, higher values tilt the camera behind the ghost");
    }
}
