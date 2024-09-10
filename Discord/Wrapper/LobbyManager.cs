using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace TNRD.Zeepkist.GTR.Discord.Wrapper
{
    public partial class LobbyManager
    {
        public IEnumerable<User> GetMemberUsers(Int64 lobbyID)
        {
            int memberCount = MemberCount(lobbyID);
            List<User> members = new List<User>();
            for (int i = 0; i < memberCount; i++)
            {
                members.Add(GetMemberUser(lobbyID, GetMemberUserId(lobbyID, i)));
            }

            return members;
        }

        public void SendLobbyMessage(Int64 lobbyID, string data, SendLobbyMessageHandler handler)
        {
            SendLobbyMessage(lobbyID, Encoding.UTF8.GetBytes(data), handler);
        }
    }
}