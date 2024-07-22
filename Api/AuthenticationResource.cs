namespace TNRD.Zeepkist.GTR.Api;

public class AuthenticationResource
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public string AccessTokenExpiry { get; set; }
    public string RefreshTokenExpiry { get; set; }
}
