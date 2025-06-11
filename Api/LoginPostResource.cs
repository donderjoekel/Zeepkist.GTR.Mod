namespace TNRD.Zeepkist.GTR.Api;

public class LoginPostResource
{
    public string ModVersion { get; set; } = null!;
    public string SteamId { get; set; }
    public string AuthenticationTicket { get; set; } = null!;
}
