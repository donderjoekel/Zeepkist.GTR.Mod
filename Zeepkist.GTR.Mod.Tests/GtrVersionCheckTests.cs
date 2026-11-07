using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using StrawberryShake;
using StrawberryShake.Transport.WebSockets;
using TNRD.Zeepkist.GTR.GraphQL;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class GtrVersionCheckTests
{
    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("2.0.0", false)]
    public async Task ExecutesRealGeneratedQueryWithoutWebSocketServices(string currentVersion, bool unsupported)
    {
        using var transport = new VersionResponseHandler();
        using var services = CreateServices(transport);
        Assert.Null(services.GetService<ISessionPool>());

        var result = await GtrVersionCheck.ExecuteAsync(services, currentVersion, default);

        Assert.Equal(unsupported, result.IsUnsupported);
        Assert.Equal("2.1.0", result.Latest);
        Assert.Equal(1, transport.Requests);
        Assert.Contains("GTR_Version_Check", transport.Body);
        Assert.Contains("minimum", transport.Body);
        Assert.Contains("latest", transport.Body);
        Assert.Null(services.GetService<ISessionPool>());
    }

    [Fact]
    public async Task GraphqlErrorsAreRejectedEvenWhenResponseContainsVersions()
    {
        using var transport = new VersionResponseHandler { IncludeErrors = true };
        using var services = CreateServices(transport);
        await Assert.ThrowsAsync<GraphQLClientException>(() =>
            GtrVersionCheck.ExecuteAsync(services, "1.0.0", default));
        Assert.Equal(1, transport.Requests);
    }

    private static ServiceProvider CreateServices(VersionResponseHandler transport)
    {
        var services = new ServiceCollection();
        services.AddGtrClient(ExecutionStrategy.NetworkOnly)
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://graphql.example/"),
                client => client.ConfigurePrimaryHttpMessageHandler(() => transport));
        return services.BuildServiceProvider();
    }

    private sealed class VersionResponseHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public string Body { get; private set; }
        public bool IncludeErrors { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Body = await request.Content.ReadAsStringAsync(cancellationToken);
            string errors = IncludeErrors ? ",\"errors\":[{\"message\":\"Version check failed\"}]" : "";
            string json = "{\"data\":{\"versions\":{\"__typename\":\"VersionsConnection\",\"nodes\":[" +
                          "{\"__typename\":\"Version\",\"minimum\":\"2.0.0\",\"latest\":\"2.1.0\"}]}}" + errors + "}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
