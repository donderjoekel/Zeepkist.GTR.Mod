namespace TNRD.Zeepkist.GTR.Api;

public class LoginPostResource
{
    public string ModVersion { get; set; } = null!;
    public ulong SteamId { get; set; }
    public string AuthenticationTicket { get; set; } = null!;
}
