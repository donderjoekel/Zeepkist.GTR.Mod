using BepInEx.Configuration;
using TNRD.Zeepkist.GTR.Core;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Configuration;

public class ConfigService : IEagerService
{
    public ConfigEntry<bool> EnableRecords { get; private set; }
    public ConfigEntry<bool> EnableGhosts { get; private set; }
    public ConfigEntry<bool> EnableVoting { get; private set; }
    public ConfigEntry<bool> ShowGhosts { get; private set; }
    public ConfigEntry<bool> ShowGhostNames { get; private set; }
    public ConfigEntry<bool> ShowGhostTransparent { get; private set; }
    public ConfigEntry<bool> ShowRecordSetMessage { get; private set; }
    public ConfigEntry<bool> ShowRecordHolder { get; private set; }
    public ConfigEntry<bool> ShowWorldRecordOnHolder { get; private set; }
    public ConfigEntry<bool> ShowPersonalBestOnHolder { get; private set; }
    public ConfigEntry<KeyCode> ToggleEnableGhosts { get; private set; }
    public ConfigEntry<KeyCode> ToggleEnableRecords { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowGhosts { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowGhostNames { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowGhostTransparent { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowRecordSetMessage { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowRecordHolder { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowWorldRecordOnHolder { get; private set; }
    public ConfigEntry<KeyCode> ToggleShowPersonalBestOnHolder { get; private set; }
    public ConfigEntry<bool> ShowOfflineWorldRecord { get; private set; }
    public ConfigEntry<bool> ShowOfflinePersonalBest { get; private set; }
    public ConfigEntry<string> ApiUrl { get; private set; }
    public ConfigEntry<string> JsonApiUrl { get; private set; }
    public ConfigEntry<string> GraphQLApiUrl { get; private set; }
    public ConfigEntry<bool> ButtonLinkDiscord { get; private set; }
    public ConfigEntry<bool> ButtonUnlinkDiscord { get; private set; }

    public ConfigService(ConfigFile config)
    {
        EnableRecords = config.Bind(
            "General",
            "_Enable Records",
            true,
            "Should records be tracked");
        EnableGhosts = config.Bind(
            "General",
            "_Enable Ghosts",
            true,
            "Should ghosts be enabled");
        EnableVoting = config.Bind(
            "General",
            "_Enable Voting",
            false,
            "Should voting be enabled");

        ShowGhosts = config.Bind(
            "Visibility",
            "_Show Ghosts",
            true,
            "Should ghosts be shown");
        ShowGhostNames = config.Bind(
            "Visibility",
            "_Show Ghost Names",
            true,
            "Should ghost names be shown");
        ShowGhostTransparent = config.Bind(
            "Visibility",
            "_Show Ghost Transparent",
            true,
            "Should ghosts be transparent");
        ShowRecordSetMessage = config.Bind(
            "Visibility",
            "_Show Record Set Message",
            true,
            "Should the record set message be shown");
        ShowRecordHolder = config.Bind(
            "Visibility",
            "_Show World Record Holder",
            true,
            "Should the record holder be shown");
        ShowWorldRecordOnHolder = config.Bind(
            "Visibility",
            "_Show World Record On Holder",
            true,
            "Should the world record be shown on the record holder");
        ShowPersonalBestOnHolder = config.Bind(
            "Visibility",
            "_Show Personal Best On Holder",
            true,
            "Should the personal best be shown on the record holder");

        ApiUrl = config.Bind(
            "URLs",
            "_The API address",
            "https://backend.zeepkist-gtr.com",
            "Allows you to set a custom API address");

        GraphQLApiUrl = config.Bind(
            "URLs",
            "_The GraphQL API address",
            "https://graphql.zeepkist-gtr.com",
            "Allows you to set a custom GraphQL API address");

        ButtonLinkDiscord = config.Bind(
            "Discord",
            "_Link",
            true,
            "[Button] Show the link discord button");
        ButtonUnlinkDiscord = config.Bind(
            "Discord",
            "_Unlink",
            true,
            "[Button] Show the unlink discord button");

        ToggleEnableRecords = config.Bind(
            "Keys",
            "_Toggle Enable Records",
            KeyCode.None,
            "Toggles if records should be enabled");
        ToggleEnableGhosts = config.Bind(
            "Keys",
            "_Toggle Enable Ghosts",
            KeyCode.None,
            "Toggles if ghosts should be enabled");

        ToggleShowGhosts = config.Bind(
            "Keys",
            "_Toggle Ghost Model Visibility",
            KeyCode.None,
            "Toggles the ghost visibility");
        ToggleShowGhostNames = config.Bind(
            "Keys",
            "_Toggle Ghost Name Visibility",
            KeyCode.None,
            "Toggles the ghost name visibility");
        ToggleShowGhostTransparent = config.Bind(
            "Keys",
            "_Toggle Ghost Transparency",
            KeyCode.None,
            "Toggles the ghost transparency");

        ToggleShowRecordSetMessage = config.Bind(
            "Keys",
            "_Toggle Record Set Message Visibility",
            KeyCode.None,
            "Toggles the record set message visibility");
        ToggleShowRecordHolder = config.Bind(
            "Keys",
            "_Toggle World Record Holder Visibility",
            KeyCode.None,
            "Toggles the world record holder visibility");

        ShowOfflineWorldRecord = config.Bind(
            "Ghosts (Offline)",
            "_Show World Record",
            true,
            "Should the world record ghost be shown");
        ShowOfflinePersonalBest = config.Bind(
            "Ghosts (Offline)",
            "_Show Personal Best",
            true,
            "Should the personal best ghost be shown");
    }
}
