using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.UI;
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

    private readonly RecordHolderGraphqlService _recordHolderGraphqlService;
    private readonly ILogger<WorldRecordCommand> _logger;

    public WorldRecordCommand()
    {
        _recordHolderGraphqlService = ServiceHelper.Instance.GetRequiredService<RecordHolderGraphqlService>();
        _logger = ServiceHelper.Instance.GetRequiredService<ILogger<WorldRecordCommand>>();
    }

    public void Handle(string arguments)
    {
        HandleAsync(arguments).Forget();
    }

    private async UniTaskVoid HandleAsync(string arguments)
    {
        Result<IGetWorldRecordHolder_WorldRecordGlobals_Nodes> result =
            await _recordHolderGraphqlService.GetWorldRecordHolder(LevelApi.CurrentHash, CancellationToken.None);

        if (result.IsFailed)
        {
            ChatApi.AddLocalMessage("[GTR] Failed to get world record");
            _logger.LogError("Failed to get world record: {Result}", result);
            return;
        }

        IGetWorldRecordHolder_WorldRecordGlobals_Nodes nodes = result.Value;

        if (nodes.Record == null)
        {
            ChatApi.AddLocalMessage("[GTR]No world records set yet");
            return;
        }

        ChatApi.SendMessage(
            "World record: " + nodes.Record.Time.GetFormattedTime() + " by " +
            nodes.Record.User!.SteamName);
    }
}
