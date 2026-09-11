using System.Net.Http;
using TNRD.Zeepkist.GTR.Connectivity;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class NetworkUserAgentTests
{
    [Fact]
    public void ValueUsesGtrBuildAndLoadedZeepSdkVersions()
    {
        NetworkUserAgent userAgent = new("1.7.0", "2.6.1");

        Assert.Equal("ZeepkistGTR/1.7.0 (ZeepSDK 2.6.1)", userAgent.Value);
    }

    [Fact]
    public void ApplyReplacesExistingHttpUserAgent()
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ExistingClient/1.0");
        NetworkUserAgent userAgent = new("1.7.0", "2.6.1");

        userAgent.Apply(client);

        Assert.Equal(
            "ZeepkistGTR/1.7.0 (ZeepSDK 2.6.1)",
            client.DefaultRequestHeaders.UserAgent.ToString());
    }

    [Fact]
    public void ApplySetsWebSocketHeader()
    {
        string name = null;
        string value = null;
        NetworkUserAgent userAgent = new("1.7.0", "2.6.1");

        userAgent.Apply((headerName, headerValue) =>
        {
            name = headerName;
            value = headerValue;
        });

        Assert.Equal("User-Agent", name);
        Assert.Equal("ZeepkistGTR/1.7.0 (ZeepSDK 2.6.1)", value);
    }
}
