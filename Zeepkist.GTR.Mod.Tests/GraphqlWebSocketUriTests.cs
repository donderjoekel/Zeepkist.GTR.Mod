using TNRD.Zeepkist.GTR.Configuration;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class GraphqlWebSocketUriTests
{
    [Theory]
    [InlineData("https://graphql.zeepki.st", "wss://graphql.zeepki.st/")]
    [InlineData("https://example.test/graphql", "wss://example.test/graphql")]
    [InlineData("http://127.0.0.1:5000/", "ws://127.0.0.1:5000/")]
    public void ConvertsHttpSchemesAndPreservesAddress(string source, string expected)
    {
        Assert.Equal(expected, GraphqlWebSocketUri.FromHttp(new Uri(source)).ToString());
    }

    [Fact]
    public void RejectsUnsupportedScheme()
    {
        Assert.Throws<ArgumentException>(() => GraphqlWebSocketUri.FromHttp(new Uri("ftp://example.test")));
    }
}
