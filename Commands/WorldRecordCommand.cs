using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Extensions;
using TNRD.Zeepkist.GTR.Utilities;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Level;

namespace TNRD.Zeepkist.GTR.Commands;

public class WorldRecordCommand : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "wr";

    public string Description =>
        "Shows the world record for the current level\n" +
        "Optionally you can pass year, quarter, month, week, or day (or the first letter of the word) as an argument to get it for that timeframe";

    private readonly WorldRecordCommandGraphQlService _service;
    private readonly ILogger<WorldRecordCommand> _logger;

    public WorldRecordCommand()
    {
        _service = ServiceHelper.Instance.GetRequiredService<WorldRecordCommandGraphQlService>();
        _logger = ServiceHelper.Instance.GetRequiredService<ILogger<WorldRecordCommand>>();
    }

    public void Handle(string arguments)
    {
        HandleAsync(arguments).Forget();
    }

    private async UniTaskVoid HandleAsync(string arguments)
    {
        string levelHash = LevelApi.GetLevelHash(LevelApi.CurrentLevel);
        Result<WorldRecords> result = await _service.GetWorldRecord(levelHash);

        if (result.IsFailed)
        {
            ChatApi.AddLocalMessage("[GTR] Failed to get world record");
            _logger.LogError("Failed to get world record: {Result}", result);
            return;
        }

        WorldRecords worldRecords = result.Value;
        if (worldRecords.Global == null)
        {
            ChatApi.AddLocalMessage("[GTR]No world records set yet");
            return;
        }

        if (string.IsNullOrEmpty(arguments))
        {
            ChatApi.SendMessage(
                "World record: " + worldRecords.Global.Time.GetFormattedTime() + " by " +
                worldRecords.Global.SteamName);
        }
        else if (arguments.EqualsAny(StringComparison.OrdinalIgnoreCase, "year", "y"))
        {
            ChatApi.SendMessage(
                "World record (Year): " + worldRecords.Yearly.Time.GetFormattedTime() + " by " +
                worldRecords.Yearly.SteamName);
        }
        else if (arguments.EqualsAny(StringComparison.OrdinalIgnoreCase, "quarter", "q"))
        {
            ChatApi.SendMessage(
                "World record (Quarter): " + worldRecords.Quarterly.Time.GetFormattedTime() + " by " +
                worldRecords.Quarterly.SteamName);
        }
        else if (arguments.EqualsAny(StringComparison.OrdinalIgnoreCase, "month", "m"))
        {
            ChatApi.SendMessage(
                "World record (Month): " + worldRecords.Monthly.Time.GetFormattedTime() + " by " +
                worldRecords.Monthly.SteamName);
        }
        else if (arguments.EqualsAny(StringComparison.OrdinalIgnoreCase, "week", "w"))
        {
            ChatApi.SendMessage(
                "World record (Week): " + worldRecords.Weekly.Time.GetFormattedTime() + " by " +
                worldRecords.Weekly.SteamName);
        }
        else if (arguments.EqualsAny(StringComparison.OrdinalIgnoreCase, "day", "d"))
        {
            ChatApi.SendMessage(
                "World record (Day): " + worldRecords.Daily.Time.GetFormattedTime() + " by " +
                worldRecords.Daily.SteamName);
        }
    }
}
