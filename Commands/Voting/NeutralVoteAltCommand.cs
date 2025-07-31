using System;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Utilities;
using TNRD.Zeepkist.GTR.Voting;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace TNRD.Zeepkist.GTR.Commands.Voting;

public class NeutralVoteAltCommand : ILocalChatCommand
{
    private readonly VotingService _votingService;

    public string Prefix => string.Empty;
    public string Command => "+-";
    public string Description => "Submits a neutral vote for the current map";

    public NeutralVoteAltCommand()
    {
        _votingService = ServiceHelper.Instance.GetRequiredService<VotingService>();
    }

    public void Handle(string arguments)
    {
        if (!string.IsNullOrEmpty(arguments))
            ChatApi.SendMessage(Command + " " + arguments);
        else
            _votingService.NeutralVote();
    }
}
