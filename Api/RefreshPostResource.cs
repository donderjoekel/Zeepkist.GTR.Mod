namespace TNRD.Zeepkist.GTR.Api;

public class RefreshPostResource
{
    public string ModVersion { get; set; } = null!;
    public string SteamId { get; set; }
    public string LoginToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
}
