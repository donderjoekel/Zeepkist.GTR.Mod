using BepInEx.Configuration;
using TNRD.Zeepkist.GTR.Core;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Configuration;

public class ConfigService : IEagerService
{
    public ConfigEntry<bool> SubmitRecords { get; private set; }
    public ConfigEntry<bool> SubmitAnyPercentRecords { get; private set; }
    public ConfigEntry<bool> ShowRecordSubmitMessage { get; private set; }

    public ConfigEntry<KeyCode> ToggleEnableRecords { get; private set; }
    public ConfigEntry<KeyCode> ToggleSubmitAnyPercentRecords { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowRecordSubmitMessage { get; private set; }

    public ConfigEntry<bool> EnableGhosts { get; private set; }

    public ConfigEntry<bool> ShowGhosts { get; private set; }
    public ConfigEntry<bool> ShowGhostNames { get; private set; }
    public ConfigEntry<bool> ShowGhostTransparent { get; private set; }
    public ConfigEntry<bool> ShowGlobalPersonalBest { get; private set; }
    public ConfigEntry<bool> ShowYearlyPersonalBest { get; private set; }
    public ConfigEntry<bool> ShowQuarterlyPersonalBest { get; private set; }
    public ConfigEntry<bool> ShowMonthlyPersonalBest { get; private set; }
    public ConfigEntry<bool> ShowWeeklyPersonalBest { get; private set; }
    public ConfigEntry<bool> ShowDailyPersonalBest { get; private set; }

    public ConfigEntry<KeyCode> ToggleEnableGhosts { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowGhosts { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowGhostNames { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowGhostTransparent { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowGlobalPersonalBest { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowYearlyPersonalBest { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowQuarterlyPersonalBest { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowMonthlyPersonalBest { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowWeeklyPersonalBest { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowDailyPersonalBest { get; private set; }

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

    public ConfigEntry<string> ApiUrl { get; private set; }
    public ConfigEntry<string> GraphQlApiUrl { get; private set; }

    public ConfigService(ConfigFile config)
    {
        ConfigRecords(config);
        ConfigGhosts(config);
        ConfigRecordHolder(config);
        ConfigDiscord(config);
        ConfigUrls(config);
    }

    private void ConfigRecords(ConfigFile config)
    {
        SubmitRecords = config.Bind(
            "1. Records - General",
            "1. Submit Records",
            true,
            "Should records be submitted");
        SubmitAnyPercentRecords = config.Bind(
            "1. Records - General",
            "2. Submit Any Percent Records",
            true,
            "Should any percent records be submitted");
        ShowRecordSubmitMessage = config.Bind(
            "1. Records - General",
            "3. Show Record Submit Message",
            true,
            "Should the record submit message be shown");

        ToggleEnableRecords = config.Bind(
            "1.1 Records - Keys",
            "1. Toggle Enable Records",
            KeyCode.None,
            "Toggles if records should be enabled");
        ToggleSubmitAnyPercentRecords = config.Bind(
            "1.1 Records - Keys",
            "2. Toggle Submit Any Percent Records",
            KeyCode.None,
            "Toggles if any percent records should be submitted");
        ToggleShowRecordSubmitMessage = config.Bind(
            "1.1 Records - Keys",
            "3. Toggle Show Record Submit Message",
            KeyCode.None,
            "Toggles if the record submit message should be shown");
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
        ShowYearlyPersonalBest = config.Bind(
            "2.1 Ghosts - Visibility",
            "5. Show Yearly Personal Best",
            true,
            "Should the yearly personal best be shown");
        ShowQuarterlyPersonalBest = config.Bind(
            "2.1 Ghosts - Visibility",
            "6. Show Quarterly Personal Best",
            true,
            "Should the quarterly personal best be shown");
        ShowMonthlyPersonalBest = config.Bind(
            "2.1 Ghosts - Visibility",
            "7. Show Monthly Personal Best",
            true,
            "Should the monthly personal best be shown");
        ShowWeeklyPersonalBest = config.Bind(
            "2.1 Ghosts - Visibility",
            "8. Show Weekly Personal Best",
            true,
            "Should the weekly personal best be shown");
        ShowDailyPersonalBest = config.Bind(
            "2.1 Ghosts - Visibility",
            "9. Show Daily Personal Best",
            true,
            "Should the daily personal best be shown");

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
        ToggleShowYearlyPersonalBest = config.Bind(
            "2.2 Ghosts - Keys",
            "6. Toggle Show Yearly Personal Best",
            KeyCode.None,
            "Toggles if the yearly personal best should be shown");
        ToggleShowQuarterlyPersonalBest = config.Bind(
            "2.2 Ghosts - Keys",
            "7. Toggle Show Quarterly Personal Best",
            KeyCode.None,
            "Toggles if the quarterly personal best should be shown");
        ToggleShowMonthlyPersonalBest = config.Bind(
            "2.2 Ghosts - Keys",
            "8. Toggle Show Monthly Personal Best",
            KeyCode.None,
            "Toggles if the monthly personal best should be shown");
        ToggleShowWeeklyPersonalBest = config.Bind(
            "2.2 Ghosts - Keys",
            "9. Toggle Show Weekly Personal Best",
            KeyCode.None,
            "Toggles if the weekly personal best should be shown");
        ToggleShowDailyPersonalBest = config.Bind(
            "2.2 Ghosts - Keys",
            "10. Toggle Show Daily Personal Best",
            KeyCode.None,
            "Toggles if the daily personal best should be shown");
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
        ApiUrl = config.Bind(
            "5. URLs",
            "1. The API address",
            "https://backend.zeepkist-gtr.com",
            "Allows you to set a custom API address\n" +
            "Changing this requires a restart of the game");
        GraphQlApiUrl = config.Bind(
            "5. URLs",
            "2. The GraphQL API address",
            "https://graphql.zeepkist-gtr.com",
            "Allows you to set a custom GraphQL API address\n" +
            "Changing this requires a restart of the game");
    }
}
