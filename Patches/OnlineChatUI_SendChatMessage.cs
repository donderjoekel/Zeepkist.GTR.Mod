using System.Collections.Generic;
using HarmonyLib;
using TNRD.Zeepkist.GTR.Mod.ChatCommands;

namespace TNRD.Zeepkist.GTR.Mod.Patches;

[HarmonyPatch(typeof(OnlineChatUI), nameof(OnlineChatUI.SendChatMessage))]
public class OnlineChatUI_SendChatMessage
{
    private static readonly List<IChatCommand> chatCommands = new List<IChatCommand>()
    {
        new FavoriteChatCommand(),
        new UpvoteChatCommand(),
        new VoteChatCommand()
    };

    private static bool Prefix(OnlineChatUI __instance, string message)
    {
        if (!Plugin.ConfigEnableVoting.Value)
            return true;

        bool executedCustomCommand = false;

        foreach (IChatCommand chatCommand in chatCommands)
        {
            if (!chatCommand.CanHandle(message))
                continue;

            chatCommand.Handle(__instance, message);
            executedCustomCommand = true;
        }

        return !executedCustomCommand;
    }
}
