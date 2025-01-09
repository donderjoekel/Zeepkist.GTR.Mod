namespace TNRD.Zeepkist.GTR.Api;

public class RefreshPostResource
{
    public string ModVersion { get; set; } = null!;
    public ulong SteamId { get; set; }
    public string LoginToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
}
