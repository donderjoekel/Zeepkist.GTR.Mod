using TNRD.Zeepkist.GTR.Core;
using ZeepSDK.ChatCommands;

namespace TNRD.Zeepkist.GTR.Commands;

public class CommandsService : IEagerService
{
    public CommandsService()
    {
        ChatCommandApi.RegisterLocalChatCommand<WorldRecordCommand>();
    }
}
