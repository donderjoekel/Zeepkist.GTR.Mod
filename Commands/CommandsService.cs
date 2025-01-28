using TNRD.Zeepkist.GTR.Commands.Voting;
using TNRD.Zeepkist.GTR.Core;
using ZeepSDK.ChatCommands;

namespace TNRD.Zeepkist.GTR.Commands;

public class CommandsService : IEagerService
{
    public CommandsService()
    {
        ChatCommandApi.RegisterLocalChatCommand<WorldRecordCommand>();

        ChatCommandApi.RegisterLocalChatCommand<DownvoteCommand>();
        ChatCommandApi.RegisterLocalChatCommand<DoubleDownvoteCommand>();
        ChatCommandApi.RegisterLocalChatCommand<UpvoteCommand>();
        ChatCommandApi.RegisterLocalChatCommand<DoubleUpvoteCommand>();
    }
}
