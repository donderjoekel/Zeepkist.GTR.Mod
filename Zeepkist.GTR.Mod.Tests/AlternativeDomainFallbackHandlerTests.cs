using System.Net;
using System.Net.Http;
using TNRD.Zeepkist.GTR.Connectivity;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class AlternativeDomainFallbackHandlerTests
{
    [Fact]
    public async Task SpanishTimeoutRetriesOnceAndPromotesOnSuccess()
    {
        FakeRoutingState routingState = new(SpainTraceResult.FromResponse(HttpStatusCode.OK, "ES"));
        RecordingHandler transport = new(
            (_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException()),
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            }));
        using HttpClient client = CreateClient(routingState, ServiceEndpoint.Backend, transport);
        using HttpRequestMessage request = new(HttpMethod.Post, "https://backend.zeepki.st/auth/login?x=1")
        {
            Version = HttpVersion.Version11,
            Content = new StringContent("{\"value\":1}")
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer token");
        request.Headers.TryAddWithoutValidation("X-Test", "value");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(routingState.Promoted);
        Assert.True(AlternativeDomainFallbackHandler.WasFallbackAttempted(response));
        Assert.Equal(2, transport.Requests.Count);
        Assert.Equal("https://backend.zeepki.st/auth/login?x=1", transport.Requests[0].Uri.AbsoluteUri);
        Assert.Equal("https://es-backend.example/auth/login?x=1", transport.Requests[1].Uri.AbsoluteUri);
        Assert.All(transport.Requests, captured =>
        {
            Assert.Equal(HttpMethod.Post, captured.Method);
            Assert.Equal(HttpVersion.Version11, captured.Version);
            Assert.Equal("Bearer token", captured.Authorization);
            Assert.Equal("value", captured.TestHeader);
            Assert.Equal("{\"value\":1}", captured.Body);
            Assert.Equal("text/plain; charset=utf-8", captured.ContentType);
        });
    }

    [Fact]
    public async Task PresumedSpanishTimeoutUsesGraphQlAlternative()
    {
        FakeRoutingState routingState = new(SpainTraceResult.NoResponse());
        RecordingHandler transport = new(
            (_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException()),
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using HttpClient client = CreateClient(routingState, ServiceEndpoint.GraphQL, transport);

        using HttpResponseMessage response = await client.PostAsync(
            "https://graphql.zeepki.st/graphql?operation=test",
            new StringContent("{}"));

        Assert.True(routingState.Promoted);
        Assert.Equal(
            "https://es-graphql.example/graphql?operation=test",
            transport.Requests[1].Uri.AbsoluteUri);
    }

    [Fact]
    public async Task NonSpanishTimeoutDoesNotFallback()
    {
        FakeRoutingState routingState = new(SpainTraceResult.FromResponse(HttpStatusCode.OK, "GB"));
        RecordingHandler transport = new(
            (_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException()));
        using HttpClient client = CreateClient(routingState, ServiceEndpoint.Backend, transport);

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            client.GetAsync("https://backend.zeepki.st/test"));

        Assert.False(routingState.Promoted);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task AlternativeNonSuccessDoesNotPromoteOrRetryAgain()
    {
        FakeRoutingState routingState = new(SpainTraceResult.FromResponse(HttpStatusCode.OK, "ES"));
        RecordingHandler transport = new(
            (_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException()),
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using HttpClient client = CreateClient(routingState, ServiceEndpoint.Backend, transport);

        using HttpResponseMessage response = await client.GetAsync("https://backend.zeepki.st/test");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.False(routingState.Promoted);
        Assert.True(AlternativeDomainFallbackHandler.WasFallbackAttempted(response));
        Assert.Equal(2, transport.Requests.Count);
    }

    [Fact]
    public async Task AlternativeFailureIsWrappedAndNotRetriedAgain()
    {
        FakeRoutingState routingState = new(SpainTraceResult.FromResponse(HttpStatusCode.OK, "ES"));
        RecordingHandler transport = new(
            (_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException()),
            (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("blocked")));
        using HttpClient client = CreateClient(routingState, ServiceEndpoint.Backend, transport);

        await Assert.ThrowsAsync<AlternativeDomainFallbackException>(() =>
            client.GetAsync("https://backend.zeepki.st/test"));

        Assert.False(routingState.Promoted);
        Assert.Equal(2, transport.Requests.Count);
    }

    [Fact]
    public async Task CallerCancellationDoesNotFallback()
    {
        FakeRoutingState routingState = new(SpainTraceResult.FromResponse(HttpStatusCode.OK, "ES"));
        TaskCompletionSource<bool> requestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingHandler transport = new(async (_, cancellationToken) =>
        {
            requestStarted.SetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using HttpClient client = CreateClient(routingState, ServiceEndpoint.Backend, transport);
        using CancellationTokenSource cancellationTokenSource = new();

        Task<HttpResponseMessage> request = client.GetAsync(
            "https://backend.zeepki.st/test",
            cancellationTokenSource.Token);
        await requestStarted.Task;
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);

        Assert.False(routingState.Promoted);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task RequestAlreadyUsingAlternativeDoesNotFallback()
    {
        FakeRoutingState routingState = new(SpainTraceResult.FromResponse(HttpStatusCode.OK, "ES"));
        RecordingHandler transport = new(
            (_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException()));
        using HttpClient client = CreateClient(routingState, ServiceEndpoint.Backend, transport);

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            client.GetAsync("https://es-backend.example/test"));

        Assert.Single(transport.Requests);
    }

    [Fact]
    public void RequestTimeoutIsTenSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), AlternativeDomainFallbackHandler.RequestTimeout);
    }

    private static HttpClient CreateClient(
        FakeRoutingState routingState,
        ServiceEndpoint endpoint,
        RecordingHandler transport)
    {
        AlternativeDomainFallbackHandler handler = new(
            routingState,
            endpoint,
            () => new HttpClient(transport, false))
        {
            InnerHandler = new NotUsedHandler()
        };
        return new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    private sealed class FakeRoutingState : ISpainRoutingState
    {
        private readonly SpainTraceResult _traceResult;

        public bool Promoted { get; private set; }

        public FakeRoutingState(SpainTraceResult traceResult)
        {
            _traceResult = traceResult;
        }

        public Task<SpainTraceResult> GetTraceResultAsync()
        {
            return Task.FromResult(_traceResult);
        }

        public bool IsProductionRequest(ServiceEndpoint endpoint, Uri requestUri)
        {
            string expectedHost = endpoint == ServiceEndpoint.Backend
                ? "backend.zeepki.st"
                : "graphql.zeepki.st";
            return requestUri.Host == expectedHost;
        }

        public Uri GetAlternativeRequestUri(ServiceEndpoint endpoint, Uri requestUri)
        {
            string host = endpoint == ServiceEndpoint.Backend
                ? "es-backend.example"
                : "es-graphql.example";
            UriBuilder builder = new(requestUri) { Host = host };
            return builder.Uri;
        }

        public void PromoteAlternativeDomains()
        {
            Promoted = true;
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses;

        public List<CapturedRequest> Requests { get; } = new();

        public RecordingHandler(
            params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] responses)
        {
            _responses = new Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(
                request.RequestUri,
                request.Method,
                request.Version,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Test", out IEnumerable<string> values)
                    ? values.Single()
                    : null,
                request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Content?.Headers.ContentType?.ToString()));
            return await _responses.Dequeue()(request, cancellationToken);
        }
    }

    private sealed record CapturedRequest(
        Uri Uri,
        HttpMethod Method,
        Version Version,
        string Authorization,
        string TestHeader,
        string Body,
        string ContentType);

    private sealed class NotUsedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Routing handler must use transport client");
        }
    }
}
