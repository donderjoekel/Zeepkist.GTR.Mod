using System.Net;
using System.Net.Http;
using TNRD.Zeepkist.GTR.Connectivity;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class CloudflareTraceRequestTests
{
    [Fact]
    public async Task ReusesSingleRequestResult()
    {
        int requestCount = 0;
        Uri requestUri = null;
        using HttpClient client = CreateClient((request, _) =>
        {
            requestCount++;
            requestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("loc=GB\n")
            });
        });
        CloudflareTraceRequest traceRequest = new(client);

        SpainTraceResult[] results = await Task.WhenAll(
            traceRequest.Result,
            traceRequest.Result,
            traceRequest.Result);

        Assert.Equal(1, requestCount);
        Assert.Equal("https://zeepki.st/cdn-cgi/trace", requestUri.AbsoluteUri);
        Assert.All(results, result => Assert.Equal("GB", result.CountryCode));
    }

    [Fact]
    public async Task TimeoutIsFallbackEligibleButNotConfirmedSpain()
    {
        using HttpClient client = CreateClient(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException();
        });
        client.Timeout = TimeSpan.FromMilliseconds(20);

        SpainTraceResult result = await new CloudflareTraceRequest(client).Result;

        Assert.False(result.HasHttpResponse);
        Assert.True(result.IsFallbackEligible);
        Assert.False(result.IsConfirmedSpain);
    }

    [Fact]
    public async Task TransportFailureIsFallbackEligibleButNotConfirmedSpain()
    {
        using HttpClient client = CreateClient((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("blocked")));

        SpainTraceResult result = await new CloudflareTraceRequest(client).Result;

        Assert.False(result.HasHttpResponse);
        Assert.True(result.IsFallbackEligible);
        Assert.False(result.IsConfirmedSpain);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, null)]
    [InlineData(HttpStatusCode.OK, "not a trace response")]
    public async Task HttpResponseWithoutValidLocationIsNotFallbackEligible(
        HttpStatusCode statusCode,
        string body)
    {
        using HttpClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body ?? string.Empty)
            }));

        SpainTraceResult result = await new CloudflareTraceRequest(client).Result;

        Assert.True(result.HasHttpResponse);
        Assert.False(result.IsFallbackEligible);
        Assert.False(result.IsConfirmedSpain);
    }

    [Fact]
    public async Task SpanishResponseIsConfirmedAndFallbackEligible()
    {
        using HttpClient client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("loc=ES")
            }));

        SpainTraceResult result = await new CloudflareTraceRequest(client).Result;

        Assert.Equal("ES", result.CountryCode);
        Assert.True(result.HasHttpResponse);
        Assert.True(result.IsFallbackEligible);
        Assert.True(result.IsConfirmedSpain);
    }

    [Fact]
    public void TimeoutIsOneSecond()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), CloudflareTraceRequest.Timeout);
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
    {
        return new HttpClient(new DelegateHandler(sendAsync))
        {
            BaseAddress = new Uri("https://zeepki.st/")
        };
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

        public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _sendAsync(request, cancellationToken);
        }
    }
}
