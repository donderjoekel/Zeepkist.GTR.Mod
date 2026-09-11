using TNRD.Zeepkist.GTR.Connectivity;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class CloudflareTraceParserTests
{
    [Theory]
    [InlineData("fl=793f27\nh=zeepki.st\nloc=GB\nwarp=off", "GB")]
    [InlineData("fl=793f27\r\nh=zeepki.st\r\nloc=ES\r\nwarp=off", "ES")]
    [InlineData("loc=es", "ES")]
    [InlineData("loc= gb ", "GB")]
    public void ParsesCountryCode(string response, string expected)
    {
        Assert.Equal(expected, CloudflareTraceParser.ParseCountryCode(response));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("loc=")]
    [InlineData("loc=ESP")]
    [InlineData("loc=1E")]
    [InlineData("location=ES")]
    [InlineData("Loc=ES")]
    [InlineData("colo=MAD")]
    public void RejectsMissingOrMalformedCountryCode(string response)
    {
        Assert.Null(CloudflareTraceParser.ParseCountryCode(response));
    }
}
