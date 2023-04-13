namespace TNRD.Zeepkist.GTR.Mod.ChatCommands;

public interface IChatCommand
{
    bool CanHandle(string input);
    void Handle(OnlineChatUI instance, string input);
}
